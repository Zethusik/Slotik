using System.Security.Claims;
namespace Slotik.Services;
public static class CurrentUser
{
    // Never fall back to email, query parameters or a caller-supplied owner ID.
    public static int? UserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue("userId"), out var id) && id > 0 ? id : null;
}
