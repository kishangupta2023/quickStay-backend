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

        _stripeWebhookSecret = !string.IsNullOrWhiteSpace(stripeOptions.Value.WebhookSecret)
            ? stripeOptions.Value.WebhookSecret
            : (configuration["STRIPE_WEBHOOK_SECRET"] ?? Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? string.Empty);

        _clerkWebhookSecret = !string.IsNullOrWhiteSpace(clerkOptions.Value.WebhookSecret)
            ? clerkOptions.Value.WebhookSecret
            : (configuration["CLERK_WEBHOOK_SECRET"] ?? Environment.GetEnvironmentVariable("CLERK_WEBHOOK_SECRET") ?? string.Empty);
    }

    [HttpPost("clerk")]
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
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var signatureHeader = Request.Headers["Stripe-Signature"];
            var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _stripeWebhookSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session != null && session.Metadata.TryGetValue("bookingId", out var bookingId) && !string.IsNullOrEmpty(bookingId))
                {
                    var update = Builders<Booking>.Update
                        .Set(b => b.Status, "confirmed")
                        .Set(b => b.IsPaid, true)
                        .Set(b => b.PaymentMethod, "Stripe");

                    await _context.Bookings.UpdateOneAsync(b => b.Id == bookingId, update);
                    _logger.LogInformation("Booking {BookingId} confirmed via Stripe webhook", bookingId);
                    
                    var booking = await _context.Bookings.Find(b => b.Id == bookingId).FirstOrDefaultAsync();
                    if (booking != null)
                    {
                        var user = await _context.Users.Find(u => u.Id == booking.User).FirstOrDefaultAsync();
                        if (user != null)
                        {
                            await _emailService.SendEmailAsync(user.Email, "Booking Confirmed - QuickStay", $"Your booking {bookingId} has been confirmed!");
                        }
                    }
                }
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe signature validation failed");
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
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
