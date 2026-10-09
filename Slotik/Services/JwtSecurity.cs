using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Slotik.Data;

namespace Slotik.Services;

public static class JwtSecurity
{
    public static void Configure(JwtBearerOptions options, IConfigurationSection settings)
    {
        var key = settings["SecretKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Supply JwtSettings:SecretKey (at least 32 bytes) through external runtime configuration.");
        if (string.IsNullOrWhiteSpace(settings["Issuer"]) || string.IsNullOrWhiteSpace(settings["Audience"]))
            throw new InvalidOperationException("JwtSettings:Issuer and Audience are required.");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings["Issuer"], ValidAudience = settings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
        options.Events = new JwtBearerEvents { OnTokenValidated = ValidateCurrentUserAsync };
    }

    public static async Task ValidateCurrentUserAsync(TokenValidatedContext context)
    {
        if (context.Principal?.UserId() is not int id)
        {
            context.Fail("Missing stable user identity."); return;
        }
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new { u.Role, IsBlocked = u.Master != null && u.Master.IsBlocked })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (user == null || user.IsBlocked || !Enum.IsDefined(user.Role))
        {
            context.Fail("Account unavailable."); return;
        }
        // Replace all token role claims with the current database role before authorization.
        var identity = (ClaimsIdentity)context.Principal.Identity!;
        foreach (var claim in identity.Claims.Where(c => c.Type == identity.RoleClaimType || c.Type == "role").ToList())
            identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(identity.RoleClaimType, user.Role.ToString()));
    }
}
