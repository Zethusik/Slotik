using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Slotik.Models;

namespace Slotik.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config) => _config = config;

        public string GenerateToken(string email, string role, int userId)
        {
            var jwtsettings = _config.GetSection("JwtSettings");

            var secretKey = jwtsettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

            var issuer = jwtsettings["Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");

            var audience = jwtsettings["Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");

            var expiryValue = jwtsettings["ExpiryInMinutes"] ?? throw new InvalidOperationException("JwtSettings:ExpiryInMinutes is not configured.");

            if (!double.TryParse( expiryValue,out var expiryInMinutes))
            {
                throw new InvalidOperationException(
                    "JwtSettings:ExpiryInMinutes is invalid.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),
                new Claim("userId", userId.ToString()),
                new Claim(ClaimTypes.Role,role)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryInMinutes),
                signingCredentials: creds
                );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
