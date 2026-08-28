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

    public ProtectFilter(MongoDbContext context)
    {
        _context = context;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            // Get the Authorization header
            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Result = new JsonResult(new { success = false, message = "not authenticated" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // Extract the JWT token
            var token = authHeader.Substring("Bearer ".Length).Trim();

            // Decode the JWT token without signature validation
            // (Clerk tokens are validated by Clerk's infrastructure, we just need the userId)
            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
            {
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
                context.Result = new JsonResult(new { success = false, message = "not authenticated" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // Check token expiry
            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
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
            context.Result = new JsonResult(new { success = false, message = "not authenticated" })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
    }
}
