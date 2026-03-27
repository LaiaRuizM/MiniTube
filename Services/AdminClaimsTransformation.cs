using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace MiniTube.Services;

public class AdminClaimsTransformation : IClaimsTransformation
{
    private readonly string _adminEmail;

    public AdminClaimsTransformation(IConfiguration config)
    {
        _adminEmail = config["AdminEmail"] ?? "";
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity == null || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        // If user's email matches admin email, add IsAdmin claim
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email) &&
            email.Equals(_adminEmail, StringComparison.OrdinalIgnoreCase) &&
            !principal.HasClaim("IsAdmin", "true"))
        {
            identity.AddClaim(new Claim("IsAdmin", "true"));
        }

        return Task.FromResult(principal);
    }
}
