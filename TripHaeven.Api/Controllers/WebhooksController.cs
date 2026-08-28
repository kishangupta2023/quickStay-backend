using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Stripe;
using System.Text.Json;
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

    public WebhooksController(MongoDbContext context, IConfiguration configuration, EmailService emailService)
    {
        _context = context;
        _stripeWebhookSecret = configuration["Stripe:WebhookSecret"] ?? configuration["STRIPE_WEBHOOK_SECRET"] ?? Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";
        _clerkWebhookSecret = configuration["Clerk:WebhookSecret"] ?? configuration["CLERK_WEBHOOK_SECRET"] ?? Environment.GetEnvironmentVariable("CLERK_WEBHOOK_SECRET") ?? "";
        _emailService = emailService;
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
                return BadRequest("Missing svix headers");
            }

            // Verify svix signature
            bool isValid = VerifySvixSignature(json, svixId, svixTimestamp, svixSignatureHeader, _clerkWebhookSecret);
            if (!isValid)
            {
                return BadRequest("Invalid signature");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            var data = root.GetProperty("data");

            var id = data.GetProperty("id").GetString()!;
            var email = data.GetProperty("email_addresses")[0].GetProperty("email_address").GetString()!;
            var username = data.GetProperty("first_name").GetString() + " " + data.GetProperty("last_name").GetString();
            var image = data.GetProperty("image_url").GetString()!;

            var userData = new User
            {
                Id = id,
                Email = email,
                Username = username,
                Image = image
            };

            switch (type)
            {
                case "user.created":
                    await _context.Users.InsertOneAsync(userData);
                    break;
                case "user.updated":
                    var update = Builders<User>.Update
                        .Set(u => u.Email, email)
                        .Set(u => u.Username, username)
                        .Set(u => u.Image, image)
                        .Set(u => u.UpdatedAt, DateTime.UtcNow);
                    await _context.Users.UpdateOneAsync(u => u.Id == id, update);
                    break;
                case "user.deleted":
                    await _context.Users.DeleteOneAsync(u => u.Id == id);
                    break;
            }

            return Ok(new { success = true, message = "Webhook Recieved" });
        }
        catch (Exception ex)
        {
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
                if (session != null)
                {
                    var bookingId = session.Metadata["bookingId"];
                    if (!string.IsNullOrEmpty(bookingId))
                    {
                        var update = Builders<Booking>.Update
                            .Set(b => b.Status, "confirmed")
                            .Set(b => b.IsPaid, true)
                            .Set(b => b.PaymentMethod, "Stripe");

                        await _context.Bookings.UpdateOneAsync(b => b.Id == bookingId, update);
                        
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
            }

            return Ok();
        }
        catch (StripeException)
        {
            return BadRequest();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return StatusCode(500);
        }
    }

    private bool VerifySvixSignature(string payload, string id, string timestamp, string signatureHeader, string secret)
    {
        if (secret.StartsWith("whsec_"))
        {
            secret = secret.Substring(6);
        }

        var key = Convert.FromBase64String(secret);
        var toSign = $"{id}.{timestamp}.{payload}";
        var toSignBytes = System.Text.Encoding.UTF8.GetBytes(toSign);

        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
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
