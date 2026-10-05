using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Stripe;
using TripHaeven.Api.Configuration;
using TripHaeven.Api.Data;
using TripHaeven.Api.Models;
using TripHaeven.Api.Services;

namespace TripHaeven.Api.Controllers;

[ApiController]
[Route("api")]
public class WebhooksController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly string _stripeWebhookSecret;
    private readonly string _clerkWebhookSecret;
    private readonly EmailService _emailService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        MongoDbContext context, 
        IConfiguration configuration, 
        IOptions<StripeSettings> stripeOptions,
        IOptions<ClerkSettings> clerkOptions,
        EmailService emailService,
        ILogger<WebhooksController> logger)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;

        var stripeSecret = configuration["STRIPE_WEBHOOK_SECRET"] 
            ?? Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")
            ?? configuration["Stripe:WebhookSecret"]
            ?? stripeOptions.Value.WebhookSecret;
        _stripeWebhookSecret = stripeSecret?.Trim() ?? string.Empty;

        var clerkSecret = configuration["CLERK_WEBHOOK_SECRET"] 
            ?? Environment.GetEnvironmentVariable("CLERK_WEBHOOK_SECRET")
            ?? configuration["Clerk:WebhookSecret"]
            ?? clerkOptions.Value.WebhookSecret;
        _clerkWebhookSecret = clerkSecret?.Trim() ?? string.Empty;
    }

    [HttpPost("clerk")]
    [HttpPost("webhooks/clerk")]
    public async Task<IActionResult> ClerkWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var svixId = Request.Headers["svix-id"].ToString();
            var svixTimestamp = Request.Headers["svix-timestamp"].ToString();
            var svixSignatureHeader = Request.Headers["svix-signature"].ToString();

            if (string.IsNullOrEmpty(svixId) || string.IsNullOrEmpty(svixTimestamp) || string.IsNullOrEmpty(svixSignatureHeader))
            {
                _logger.LogWarning("Clerk webhook missing svix headers");
                return BadRequest("Missing svix headers");
            }

            // Verify svix signature
            bool isValid = VerifySvixSignature(json, svixId, svixTimestamp, svixSignatureHeader, _clerkWebhookSecret);
            if (!isValid)
            {
                _logger.LogWarning("Clerk webhook signature verification failed");
                return BadRequest("Invalid signature");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            var data = root.GetProperty("data");

            var id = data.GetProperty("id").GetString()!;
            var email = data.GetProperty("email_addresses")[0].GetProperty("email_address").GetString()!;
            var username = (data.TryGetProperty("first_name", out var fn) ? fn.GetString() : "") + " " + 
                           (data.TryGetProperty("last_name", out var ln) ? ln.GetString() : "");
            var image = data.TryGetProperty("image_url", out var img) ? img.GetString() ?? "" : "";

            var userData = new User
            {
                Id = id,
                Email = email,
                Username = username.Trim(),
                Image = image
            };

            switch (type)
            {
                case "user.created":
                    await _context.Users.InsertOneAsync(userData);
                    _logger.LogInformation("User created via Clerk webhook: {UserId}", id);
                    break;
                case "user.updated":
                    var update = Builders<User>.Update
                        .Set(u => u.Email, email)
                        .Set(u => u.Username, username.Trim())
                        .Set(u => u.Image, image)
                        .Set(u => u.UpdatedAt, DateTime.UtcNow);
                    await _context.Users.UpdateOneAsync(u => u.Id == id, update);
                    _logger.LogInformation("User updated via Clerk webhook: {UserId}", id);
                    break;
                case "user.deleted":
                    await _context.Users.DeleteOneAsync(u => u.Id == id);
                    _logger.LogInformation("User deleted via Clerk webhook: {UserId}", id);
                    break;
            }

            return Ok(new { success = true, message = "Webhook Recieved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Clerk webhook");
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("stripe")]
    [HttpPost("webhooks/stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();

        _logger.LogInformation("Stripe webhook received. Body length: {BodyLength}, HasSignature: {HasSig}, SecretConfigured: {HasSecret}", 
            json?.Length ?? 0, 
            !string.IsNullOrEmpty(signatureHeader),
            !string.IsNullOrEmpty(_stripeWebhookSecret));

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _stripeWebhookSecret, throwOnApiVersionMismatch: false);

            _logger.LogInformation("Processing Stripe event: {EventType}, EventId: {EventId}", stripeEvent.Type, stripeEvent.Id);

            string? bookingId = null;
            string? customerEmail = null;
            string? customerName = null;

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session != null)
                {
                    session.Metadata?.TryGetValue("bookingId", out bookingId);
                    customerEmail = session.CustomerDetails?.Email ?? session.CustomerEmail;
                    customerName = session.CustomerDetails?.Name;
                }
            }
            else if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded)
            {
                var paymentIntent = stripeEvent.Data.Object as Stripe.PaymentIntent;
                if (paymentIntent != null)
                {
                    paymentIntent.Metadata?.TryGetValue("bookingId", out bookingId);
                    customerEmail = paymentIntent.ReceiptEmail;
                }
            }
            else if (stripeEvent.Type == EventTypes.ChargeSucceeded)
            {
                var charge = stripeEvent.Data.Object as Stripe.Charge;
                if (charge != null)
                {
                    charge.Metadata?.TryGetValue("bookingId", out bookingId);
                    customerEmail = charge.BillingDetails?.Email ?? charge.ReceiptEmail;
                    customerName = charge.BillingDetails?.Name;
                }
            }

            if (!string.IsNullOrEmpty(bookingId))
            {
                var booking = await _context.Bookings.Find(b => b.Id == bookingId).FirstOrDefaultAsync();
                if (booking == null)
                {
                    _logger.LogWarning("Booking {BookingId} not found in database for event {EventType}", bookingId, stripeEvent.Type);
                    return Ok();
                }

                if (booking.IsPaid)
                {
                    _logger.LogInformation("Booking {BookingId} is already marked as paid. Skipping duplicate processing.", bookingId);
                    return Ok();
                }

                var update = Builders<Booking>.Update
                    .Set(b => b.Status, "confirmed")
                    .Set(b => b.IsPaid, true)
                    .Set(b => b.PaymentMethod, "Stripe");

                await _context.Bookings.UpdateOneAsync(b => b.Id == bookingId, update);
                _logger.LogInformation("Booking {BookingId} confirmed and marked as Paid via Stripe {EventType}", bookingId, stripeEvent.Type);

                // Send payment confirmation email in background so slow SMTP never blocks the webhook response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var user = await _context.Users.Find(u => u.Id == booking.User).FirstOrDefaultAsync();
                        var recipientEmail = !string.IsNullOrWhiteSpace(user?.Email) 
                            ? user.Email 
                            : (!string.IsNullOrWhiteSpace(customerEmail) ? customerEmail : string.Empty);

                        var recipientName = !string.IsNullOrWhiteSpace(user?.Username)
                            ? user.Username
                            : (!string.IsNullOrWhiteSpace(customerName) ? customerName : "Valued Guest");

                        if (string.IsNullOrWhiteSpace(recipientEmail))
                        {
                            _logger.LogWarning("Cannot send payment confirmation email: No email address found for booking {BookingId}", bookingId);
                            return;
                        }

                        string hotelName = "QuickStay Hotel";
                        string hotelAddress = "";
                        try
                        {
                            if (!string.IsNullOrEmpty(booking.Hotel))
                            {
                                var hotel = await _context.Hotels.Find(h => h.Id == booking.Hotel).FirstOrDefaultAsync();
                                if (hotel != null)
                                {
                                    hotelName = hotel.Name ?? hotelName;
                                    hotelAddress = hotel.Address ?? "";
                                }
                            }
                        }
                        catch (Exception hex)
                        {
                            _logger.LogWarning(hex, "Could not fetch hotel details for booking {BookingId}", bookingId);
                        }

                        var emailHtml = $@"
                            <h2>Payment Received & Booking Confirmed!</h2>
                            <p>Dear {recipientName},</p>
                            <p>We have successfully received your payment! Your reservation is now confirmed.</p>
                            <ul>
                              <li><strong>Booking ID:</strong> {booking.Id}</li>
                              <li><strong>Hotel Name:</strong> {hotelName}</li>
                              <li><strong>Location:</strong> {hotelAddress}</li>
                              <li><strong>Check-In Date:</strong> {booking.CheckInDate.ToShortDateString()}</li>
                              <li><strong>Check-Out Date:</strong> {booking.CheckOutDate.ToShortDateString()}</li>
                              <li><strong>Amount Paid:</strong> ${booking.TotalPrice}</li>
                              <li><strong>Payment Method:</strong> Stripe</li>
                              <li><strong>Payment Status:</strong> Confirmed & Paid</li>
                            </ul>
                            <p>Thank you for choosing TripHaeven QuickStay. We look forward to welcoming you!</p>
                            <p>If you have any questions, feel free to contact us.</p>";

                        _logger.LogInformation("Sending payment confirmation email to {RecipientEmail} for booking {BookingId}", recipientEmail, booking.Id);

                        await _emailService.SendEmailAsync(
                            recipientEmail, 
                            "Booking & Payment Confirmed - TripHaeven QuickStay", 
                            $"Your payment for booking {bookingId} has been confirmed!", 
                            emailHtml);

                        _logger.LogInformation("Payment confirmation email successfully sent to {RecipientEmail}", recipientEmail);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send payment confirmation email for booking {BookingId}", bookingId);
                    }
                });
            }
            else
            {
                _logger.LogInformation("Stripe event {EventType} received without bookingId metadata", stripeEvent.Type);
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe signature validation failed: {ErrorMessage}", ex.Message);
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook: {ErrorMessage}", ex.Message);
            return StatusCode(500);
        }
    }

    private bool VerifySvixSignature(string payload, string id, string timestamp, string signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return false;

        if (secret.StartsWith("whsec_"))
        {
            secret = secret.Substring(6);
        }

        var key = Convert.FromBase64String(secret);
        var toSign = $"{id}.{timestamp}.{payload}";
        var toSignBytes = Encoding.UTF8.GetBytes(toSign);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(toSignBytes);
        var expectedSignature = Convert.ToHexString(hash).ToLower();

        var signatures = signatureHeader.Split(' ');
        foreach (var sig in signatures)
        {
            var parts = sig.Split(',');
            if (parts.Length == 2 && parts[0] == "v1")
            {
                if (parts[1] == expectedSignature)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
