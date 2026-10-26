using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DentalLabOS.Api.Infrastructure;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "DentalLabOS";

    public string Audience { get; set; } = "DentalLabOS.Api";

    /// <summary>Base64 or plain text, at least 32 bytes. Must come from configuration/secrets — never committed.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>
    /// Resolves the signing key. In Development only, a missing key is replaced by a random per-process key
    /// (tokens die on restart). In every other environment a missing or short key stops the app.
    /// </summary>
    public SymmetricSecurityKey ResolveKey(bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException("Jwt:SigningKey is not configured. Set Jwt__SigningKey (at least 32 bytes) from a secret store.");
            }

            SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }

        var bytes = Encoding.UTF8.GetBytes(SigningKey);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
        }

        return new SymmetricSecurityKey(bytes);
    }
}

/// <summary>Issues short-lived access tokens carrying the same identity claims as the web cookie (no permissions).</summary>
public sealed class TokenIssuer(JwtOptions options, SymmetricSecurityKey key, TimeProvider time)
{
    public (string Token, DateTime ExpiresAtUtc) Issue(ClaimsPrincipal principal)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var claims = principal.Claims
            .Where(c => c.Type != "AspNet.Identity.SecurityStamp")
            .Append(new Claim("jti", Guid.NewGuid().ToString("N")));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expires);
    }
}
