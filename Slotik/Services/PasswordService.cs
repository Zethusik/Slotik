using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Slotik.Models;

namespace Slotik.Services;

public sealed class PasswordService
{
    // Identity V3: random salt, PBKDF2-HMAC-SHA512, self-describing versioned hash.
    private readonly PasswordHasher<User> _hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = 210_000
    }));

    public string Hash(string password) => _hasher.HashPassword(new User(), password);

    public PasswordVerificationResult Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password)) return PasswordVerificationResult.Failed;
        // Legacy password hashes are accepted only for verification, then upgraded on login.
        if (hash.Length == 64 && hash.All(Uri.IsHexDigit))
        {
            var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash), supplied)
                ? PasswordVerificationResult.SuccessRehashNeeded : PasswordVerificationResult.Failed;
        }
        try { return _hasher.VerifyHashedPassword(new User(), hash, password); }
        catch (FormatException) { return PasswordVerificationResult.Failed; }
    }
}
