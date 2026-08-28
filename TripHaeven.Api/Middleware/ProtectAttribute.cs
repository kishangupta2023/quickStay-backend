using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MongoDB.Driver;
using System.Security.Claims;
using TripHaeven.Api.Data;

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
        var userClaims = context.HttpContext.User;
        if (userClaims == null || !userClaims.Identity!.IsAuthenticated)
        {
            context.Result = new JsonResult(new { success = false, message = "not authenticated" })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // Clerk puts the user ID in the sub or NameIdentifier claim
        var userId = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? userClaims.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new JsonResult(new { success = false, message = "not authenticated" })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        
        if (user == null)
        {
            // If user is authenticated via Clerk but not in DB yet (webhook delay)
            context.Result = new JsonResult(new { success = false, message = "user not found in database" })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // Store user in HttpContext to be accessed by controllers
        context.HttpContext.Items["User"] = user;
    }
}
