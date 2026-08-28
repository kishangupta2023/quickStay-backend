using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TripHaeven.Api.Data;
using TripHaeven.Api.Middleware;
using TripHaeven.Api.Models;

namespace TripHaeven.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly MongoDbContext _context;

    public UserController(MongoDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Protect]
    public IActionResult GetUserData()
    {
        var user = HttpContext.Items["User"] as User;
        return Ok(new { success = true, role = user!.Role, recentSearchedCities = user.RecentSearchedCities });
    }

    [HttpPost("store-recent-search")]
    [Protect]
    public async Task<IActionResult> StoreRecentSearchedCities([FromBody] StoreSearchRequest request)
    {
        try
        {
            var user = HttpContext.Items["User"] as User;
            var city = request.RecentSearchedCity;

            if (user!.RecentSearchedCities.Contains(city))
            {
                return Ok(new { success = true, message = "City already in list" });
            }

            user.RecentSearchedCities.Add(city);
            
            // Keep only the most recent 3 searches
            if (user.RecentSearchedCities.Count > 3)
            {
                user.RecentSearchedCities.RemoveAt(0); // shift equivalent
            }

            var update = Builders<User>.Update.Set(u => u.RecentSearchedCities, user.RecentSearchedCities);
            await _context.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            return Ok(new { success = true, message = "City added" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }
}

public class StoreSearchRequest
{
    public string RecentSearchedCity { get; set; } = null!;
}
