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

    public HotelsController(MongoDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [Protect]
    public async Task<IActionResult> RegisterHotel([FromBody] HotelRegistrationRequest request)
    {
        try
        {
            var user = HttpContext.Items["User"] as User;

            var existingHotel = await _context.Hotels.Find(h => h.Owner == user!.Id).FirstOrDefaultAsync();

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
                Owner = user!.Id
            };

            await _context.Hotels.InsertOneAsync(hotel);

            // Update user role to hotelOwner
            var update = Builders<User>.Update.Set(u => u.Role, "hotelOwner");
            await _context.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            return Ok(new { success = true, message = "Hotel registered successfully", hotel });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }
}

public class HotelRegistrationRequest
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string Contact { get; set; } = null!;
    public string City { get; set; } = null!;
}
