var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<TripHaeven.Api.Data.MongoDbContext>();
builder.Services.AddSingleton<TripHaeven.Api.Services.CloudinaryService>();
builder.Services.AddSingleton<TripHaeven.Api.Services.EmailService>();
builder.Services.AddSingleton<TripHaeven.Api.Services.StripeService>();

// Configure CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "https://hotel-booking-lac-tau.vercel.app") // Ensure production URL is also allowed
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Configure Authentication
var clerkAuthority = builder.Configuration["Clerk:Authority"] ?? builder.Configuration["CLERK_AUTHORITY"] ?? Environment.GetEnvironmentVariable("CLERK_AUTHORITY");
if (string.IsNullOrEmpty(clerkAuthority))
{
    // Try to infer from Publishable Key if authority is not set
    var pubKey = builder.Configuration["Clerk:PublishableKey"] ?? builder.Configuration["CLERK_PUBLISHABLE_KEY"] ?? Environment.GetEnvironmentVariable("CLERK_PUBLISHABLE_KEY");
    if (!string.IsNullOrEmpty(pubKey) && pubKey.StartsWith("pk_test_"))
    {
        var base64 = pubKey.Substring(8).Split('$')[0];
        try {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64 + "==")); // simple padding
            clerkAuthority = $"https://{decoded}";
        } catch {}
    }
}

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = clerkAuthority ?? "https://clerk.dev"; // Fallback
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false, // Clerk doesn't usually set audience by default in simple setups
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Simple health check matching the Node.js one
app.MapGet("/", () => "API is working");

app.Run();
