var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<TripHaeven.Api.Data.MongoDbContext>();
builder.Services.AddSingleton<TripHaeven.Api.Services.CloudinaryService>();
builder.Services.AddSingleton<TripHaeven.Api.Services.EmailService>();
builder.Services.AddSingleton<TripHaeven.Api.Services.StripeService>();
builder.Services.AddHttpClient(); // For Clerk token verification

// Configure CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.SetIsOriginAllowed(origin => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.MapControllers();

// Simple health check matching the Node.js one
app.MapGet("/", () => "API is working");

app.Run();
