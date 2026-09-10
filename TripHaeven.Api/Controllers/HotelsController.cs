using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TripHaeven.Api.Data;
using TripHaeven.Api.Middleware;
using TripHaeven.Api.Models;

namespace TripHaeven.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HotelsController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly ILogger<HotelsController> _logger;

    public HotelsController(MongoDbContext context, ILogger<HotelsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    [Protect]
    public async Task<IActionResult> RegisterHotel([FromBody] HotelRegistrationRequest request)
    {
        try
        {
            var user = HttpContext.Items["User"] as User;
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "User not found" });
            }

            var existingHotel = await _context.Hotels.Find(h => h.Owner == user.Id).FirstOrDefaultAsync();

            if (existingHotel != null)
            {
                return Ok(new { success = false, message = "You have already registered a hotel." });
            }

            var hotel = new Hotel
            {
                Name = request.Name,
                Address = request.Address,
                Contact = request.Contact,
                City = request.City,
                Owner = user.Id
            };

            await _context.Hotels.InsertOneAsync(hotel);

            // Update user role to hotelOwner
            var update = Builders<User>.Update.Set(u => u.Role, "hotelOwner");
            await _context.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            _logger.LogInformation("Hotel {HotelName} registered successfully by user {UserId}", hotel.Name, user.Id);
            return Ok(new { success = true, message = "Hotel registered successfully", hotel });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while registering hotel");
            return Ok(new { success = false, message = ex.Message });
        }
    }
}

public class HotelRegistrationRequest
{
    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public string Address { get; set; } = null!;

    [Required]
    public string Contact { get; set; } = null!;

    [Required]
    public string City { get; set; } = null!;
}
