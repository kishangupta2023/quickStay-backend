using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TripHaeven.Api.Models;

[MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
public class Hotel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("address")]
    public string Address { get; set; } = null!;

    [BsonElement("contact")]
    public string Contact { get; set; } = null!;

    [BsonElement("owner")]
    public string Owner { get; set; } = null!; // String referencing User.Id

    [BsonElement("city")]
    public string City { get; set; } = null!;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
