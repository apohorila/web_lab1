using System.Security.Claims;

namespace server.Features;

public static class ClaimsExtensions
{
    public static int? GetUserId(this ClaimsPrincipal? user)
    {
        var value = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }

    public static string? GetEmail(this ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst("email")?.Value;
}
