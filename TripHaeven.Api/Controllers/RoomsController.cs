using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TripHaeven.Api.Data;
using TripHaeven.Api.Middleware;
using TripHaeven.Api.Models;
using TripHaeven.Api.Services;

namespace TripHaeven.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly CloudinaryService _cloudinaryService;

    public RoomsController(MongoDbContext context, CloudinaryService cloudinaryService)
    {
        _context = context;
        _cloudinaryService = cloudinaryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRooms([FromQuery] string? city)
    {
        try
        {
            var rooms = await _context.Rooms.Find(r => r.IsAvailable).ToListAsync();
            var hotels = await _context.Hotels.Find(_ => true).ToListAsync();

            var populatedRooms = rooms.Select(r => 
            {
                var hotel = hotels.FirstOrDefault(h => h.Id == r.Hotel);
                return new 
                {
                    _id = r.Id,
                    hotel = hotel != null ? new 
                    {
                        _id = hotel.Id,
                        name = hotel.Name,
                        address = hotel.Address,
                        contact = hotel.Contact,
                        city = hotel.City,
                        owner = hotel.Owner
                    } : null,
                    roomType = r.RoomType,
                    pricePerNight = r.PricePerNight,
                    amenities = r.Amenities,
                    images = r.Images,
                    isAvailable = r.IsAvailable,
                    createdAt = r.CreatedAt,
                    updatedAt = r.UpdatedAt
                };
            }).ToList();

            if (!string.IsNullOrEmpty(city))
            {
                populatedRooms = populatedRooms
                    .Where(pr => pr.hotel != null && pr.hotel.city.Equals(city, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Mongoose default sort is newer first
            populatedRooms = populatedRooms.OrderByDescending(r => r.createdAt).ToList();

            return Ok(new { success = true, rooms = populatedRooms });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("owner")]
    [Protect]
    public async Task<IActionResult> GetOwnerRooms()
    {
        try
        {
            var user = HttpContext.Items["User"] as User;
            var hotel = await _context.Hotels.Find(h => h.Owner == user!.Id).FirstOrDefaultAsync();

            if (hotel == null)
            {
                return Ok(new { success = false, message = "You have not registered any hotel." });
            }

            var rooms = await _context.Rooms.Find(r => r.Hotel == hotel.Id).ToListAsync();

            var populatedRooms = rooms.Select(r => new 
            {
                _id = r.Id,
                hotel = new 
                {
                    _id = hotel.Id,
                    name = hotel.Name,
                    address = hotel.Address,
                    contact = hotel.Contact,
                    city = hotel.City,
                    owner = hotel.Owner
                },
                roomType = r.RoomType,
                pricePerNight = r.PricePerNight,
                amenities = r.Amenities,
                images = r.Images,
                isAvailable = r.IsAvailable,
                createdAt = r.CreatedAt,
                updatedAt = r.UpdatedAt
            }).ToList();

            return Ok(new { success = true, rooms = populatedRooms });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Protect]
    public async Task<IActionResult> CreateRoom([FromForm] CreateRoomRequest request, IFormFileCollection images)
    {
        try
        {
            var user = HttpContext.Items["User"] as User;
            
            var hotel = await _context.Hotels.Find(h => h.Owner == user!.Id).FirstOrDefaultAsync();
            if (hotel == null)
            {
                return Ok(new { success = false, message = "No Hotel found" });
            }

            var imageUrls = new List<string>();
            if (images != null && images.Count > 0)
            {
                foreach (var image in images)
                {
                    var url = await _cloudinaryService.UploadImageAsync(image);
                    if (!string.IsNullOrEmpty(url))
                    {
                        imageUrls.Add(url);
                    }
                }
            }

            // Parse JSON array string
            var amenitiesList = new List<string>();
            if (!string.IsNullOrEmpty(request.Amenities))
            {
                try
                {
                    amenitiesList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(request.Amenities) ?? new List<string>();
                }
                catch
                {
                    amenitiesList = request.Amenities.Split(',').Select(a => a.Trim()).ToList();
                }
            }

            var room = new Room
            {
                Hotel = hotel.Id,
                RoomType = request.RoomType,
                PricePerNight = request.PricePerNight,
                Amenities = amenitiesList,
                Images = imageUrls
            };

            await _context.Rooms.InsertOneAsync(room);

            return Ok(new { success = true, message = "Room created successfully" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("toggle-availability")]
    [Protect]
    public async Task<IActionResult> ToggleRoomAvailability([FromBody] ToggleAvailabilityRequest request)
    {
        try
        {
            var room = await _context.Rooms.Find(r => r.Id == request.RoomId).FirstOrDefaultAsync();
            if (room == null)
            {
                return Ok(new { success = false, message = "Room not found." });
            }

            var update = Builders<Room>.Update.Set(r => r.IsAvailable, !room.IsAvailable);
            await _context.Rooms.UpdateOneAsync(r => r.Id == request.RoomId, update);

            return Ok(new { success = true, message = "Room availability Updated" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }
}

public class CreateRoomRequest
{
    public string RoomType { get; set; } = null!;
    public decimal PricePerNight { get; set; }
    public string Amenities { get; set; } = null!; // JSON array string
}

public class ToggleAvailabilityRequest
{
    public string RoomId { get; set; } = null!;
}
