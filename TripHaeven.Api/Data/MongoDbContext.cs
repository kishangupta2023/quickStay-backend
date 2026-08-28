using MongoDB.Driver;
using TripHaeven.Api.Models;

namespace TripHaeven.Api.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:MongoDB"] ?? configuration["MONGODB_URI"] ?? Environment.GetEnvironmentVariable("MONGODB_URI");

        var client = new MongoClient(connectionString);
        
        // Extract database name from URI, fallback to "hotel-booking" if not present in URI
        var mongoUrl = new MongoUrl(connectionString);
        _database = client.GetDatabase(mongoUrl.DatabaseName ?? "hotel-booking");
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Hotel> Hotels => _database.GetCollection<Hotel>("hotels");
    public IMongoCollection<Room> Rooms => _database.GetCollection<Room>("rooms");
    public IMongoCollection<Booking> Bookings => _database.GetCollection<Booking>("bookings");
}
