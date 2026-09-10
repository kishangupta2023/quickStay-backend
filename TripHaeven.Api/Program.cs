using Scalar.AspNetCore;
using TripHaeven.Api.Configuration;
using TripHaeven.Api.Data;
using TripHaeven.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Bind strongly-typed configuration settings (Options Pattern)
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection(MongoDbSettings.SectionName));
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection(CloudinarySettings.SectionName));
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection(StripeSettings.SectionName));
builder.Services.Configure<ClerkSettings>(builder.Configuration.GetSection(ClerkSettings.SectionName));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));

// Add controllers and OpenAPI services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register application singleton services
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<CloudinaryService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<StripeService>();

// Configure CORS for frontend client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TripHaeven QuickStay API")
               .WithTheme(ScalarTheme.Moon);
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.MapControllers();

// Health check endpoints
app.MapGet("/", () => "API is working");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.Run();
