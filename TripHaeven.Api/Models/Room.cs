using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TripHaeven.Api.Models;

[MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
public class Room
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("hotel")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Hotel { get; set; } = null!; // References Hotel.Id

    [BsonElement("roomType")]
    public string RoomType { get; set; } = null!;

    [BsonElement("pricePerNight")]
    public decimal PricePerNight { get; set; }

    [BsonElement("amenities")]
    public List<string> Amenities { get; set; } = new();

    [BsonElement("images")]
    public List<string> Images { get; set; } = new();

    [BsonElement("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
