using System.Security.Claims;

namespace RichLife.Api.Endpoints;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Player id from the token's NameIdentifier claim, or null when it is absent or
    /// malformed — so a bad token is a 401 rather than an unhandled parse exception.
    /// </summary>
    public static Guid? GetPlayerId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
