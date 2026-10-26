using System.Security.Cryptography;
using System.Text;

namespace DentalLabOS.Application.Common;

/// <summary>Random single-use tokens (invitations, setup). Only the SHA-256 hash is persisted.</summary>
public static class SecureToken
{
    public static string Create(int bytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public static class RowVersions
{
    public static string Encode(byte[]? rowVersion) => rowVersion is { Length: > 0 } ? Convert.ToBase64String(rowVersion) : string.Empty;
}
