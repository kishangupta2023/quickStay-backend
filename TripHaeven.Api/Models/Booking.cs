using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TripHaeven.Api.Models;

public class Booking
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("user")]
    public string User { get; set; } = null!; // References User.Id

    [BsonElement("room")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Room { get; set; } = null!; // References Room.Id

    [BsonElement("hotel")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Hotel { get; set; } = null!; // References Hotel.Id

    [BsonElement("checkInDate")]
    public DateTime CheckInDate { get; set; }

    [BsonElement("checkOutDate")]
    public DateTime CheckOutDate { get; set; }

    [BsonElement("totalPrice")]
    public decimal TotalPrice { get; set; }

    [BsonElement("guests")]
    public int Guests { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "pending"; // "pending", "confirmed", "cancelled"

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } = "Pay At Hotel";

    [BsonElement("isPaid")]
    public bool IsPaid { get; set; } = false;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
