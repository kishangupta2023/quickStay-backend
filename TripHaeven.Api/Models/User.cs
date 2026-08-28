using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TripHaeven.Api.Models;

public class User
{
    [BsonId]
    public string Id { get; set; } = null!; // Clerk user ID is a string

    [BsonElement("username")]
    public string Username { get; set; } = null!;

    [BsonElement("email")]
    public string Email { get; set; } = null!;

    [BsonElement("image")]
    public string Image { get; set; } = null!;

    [BsonElement("role")]
    public string Role { get; set; } = "user";

    [BsonElement("recentSearchedCities")]
    public List<string> RecentSearchedCities { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
