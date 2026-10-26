using System.Security.Cryptography;
using System.Text;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalLabOS.Infrastructure.Identity;

/// <summary>
/// AUTH-06: first platform owner without default credentials (Sprint 0, day 2). Until an owner exists, /setup accepts a
/// one-time token from configuration (<c>Bootstrap:SetupToken</c>) or, if none is configured, a random token written
/// to the server log at startup. Once an owner exists, setup is permanently closed.
/// </summary>
public sealed class SetupTokenProvider
{
    public SetupTokenProvider(IConfiguration configuration, ILogger<SetupTokenProvider> logger)
    {
        var configured = configuration["Bootstrap:SetupToken"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Token = configured.Trim();
            return;
        }

        Token = SecureToken.Create(24);
        logger.LogWarning("DentalLab OS setup token (valid until the first platform owner is created): {SetupToken}", Token);
    }

    public string Token { get; }

    public bool Matches(string? candidate) =>
        candidate is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate.Trim()), Encoding.UTF8.GetBytes(Token));
}

public sealed class PlatformBootstrapService(
    SetupTokenProvider tokens,
    IIdentityService identity,
    IAuditWriter audit,
    Persistence.AppDbContext db)
{
    public Task<bool> IsSetupOpenAsync(CancellationToken ct = default) =>
        identity.AnyPlatformOwnerAsync(ct).ContinueWith(t => !t.Result, ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    public async Task<Guid> CreateOwnerAsync(string token, string fullName, string email, string password, CancellationToken ct = default)
    {
        if (!await IsSetupOpenAsync(ct))
        {
            throw ValidationException.For("Token", "Validation.SetupClosed");
        }

        new Validator()
            .When(!tokens.Matches(token), "Token", "Validation.SetupToken")
            .Required("FullName", fullName).MaxLength("FullName", fullName, 200)
            .Required("Email", email).Email("Email", email)
            .ThrowIfInvalid();

        var userId = await identity.CreateUserAsync(email, fullName, password, null, PlatformRole.Owner, "ar-EG", ct);
        audit.Add(AuditActions.Create, "PlatformOwner", userId, "Initial setup");
        await db.SaveChangesAsync(ct);
        return userId;
    }
}
