namespace DentalLabOS.Contracts.Auth;

public sealed record TokenRequest(string Email, string Password);

public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string TokenType = "Bearer");

public sealed record MeResponse(
    Guid UserId,
    string Email,
    string FullName,
    Guid? TenantId,
    string? TenantName,
    bool IsPlatformStaff,
    string Culture,
    string Theme,
    IReadOnlyCollection<string> Permissions);
