using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using TripHaeven.Api.Data;
using TripHaeven.Api.Models;

namespace TripHaeven.Api.Middleware;

public class ProtectAttribute : TypeFilterAttribute
{
    public ProtectAttribute() : base(typeof(ProtectFilter))
    {
    }
}

public class ProtectFilter : IAsyncAuthorizationFilter
{
    private readonly MongoDbContext _context;
    private readonly ILogger<ProtectFilter> _logger;

    public ProtectFilter(MongoDbContext context, ILogger<ProtectFilter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            // Get the Authorization header
            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new JsonResult(new { success = false, message = "not authenticated" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // Extract the JWT token
            var token = authHeader.Substring("Bearer ".Length).Trim();

            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
            {
                _logger.LogWarning("Invalid JWT token format received");
                context.Result = new JsonResult(new { success = false, message = "invalid token" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            var jwtToken = handler.ReadJwtToken(token);

            // Extract the user ID from the 'sub' claim (Clerk uses 'sub' for user ID)
            var userId = jwtToken.Subject 
                         ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Token missing 'sub' claim");
                context.Result = new JsonResult(new { success = false, message = "not authenticated" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // Check token expiry
            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                _logger.LogInformation("Token expired for user {UserId}", userId);
                context.Result = new JsonResult(new { success = false, message = "token expired" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // Find the user in MongoDB
            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found in database", userId);
                context.Result = new JsonResult(new { success = false, message = "not authenticated" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // Store user in HttpContext to be accessed by controllers
            context.HttpContext.Items["User"] = user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorization filter exception");
            context.Result = new JsonResult(new { success = false, message = "not authenticated" })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
    }
}
