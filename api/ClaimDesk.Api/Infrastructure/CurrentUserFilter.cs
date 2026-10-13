using ClaimDesk.Api.Data;
using ClaimDesk.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Infrastructure;

// Demo identity: there is no login. Every request names its user in the X-User-Id header.
// This filter runs before model binding on every controller action, loads that user
// and keeps it in HttpContext.Items for the controller. Actions marked [AllowAnonymous] are skipped.
public class CurrentUserFilter(ClaimDeskDbContext db) : IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-User-Id";
    public const string ItemKey = "CurrentUser";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var header = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!int.TryParse(header, out var userId))
        {
            throw new UnauthorizedException($"The {HeaderName} header is missing or is not a number.");
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new UnauthorizedException($"User {userId} does not exist.");

        context.HttpContext.Items[ItemKey] = user;
    }
}
