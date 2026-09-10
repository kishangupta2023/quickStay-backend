using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TripHaeven.Api.Configuration;
using TripHaeven.Api.Models;

namespace TripHaeven.Api.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration, IOptions<MongoDbSettings> dbOptions)
    {
        var connectionString = dbOptions.Value.MongoDB;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration["ConnectionStrings:MongoDB"] 
                            ?? configuration["MONGODB_URI"] 
                            ?? Environment.GetEnvironmentVariable("MONGODB_URI") 
                            ?? string.Empty;
        }

        var client = new MongoClient(connectionString);
        var mongoUrl = new MongoUrl(connectionString);
        _database = client.GetDatabase(mongoUrl.DatabaseName ?? "hotel-booking");
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Hotel> Hotels => _database.GetCollection<Hotel>("hotels");
    public IMongoCollection<Room> Rooms => _database.GetCollection<Room>("rooms");
    public IMongoCollection<Booking> Bookings => _database.GetCollection<Booking>("bookings");
}
