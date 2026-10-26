using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Infrastructure.Persistence;

namespace DentalLabOS.Infrastructure.Services;

public sealed class AuditWriter(AppDbContext db, ICurrentUser currentUser, ITenantContext tenant, IClock clock) : IAuditWriter
{
    public void Add(string action, string entityType, Guid? entityId, string? reason = null, object? changes = null, Guid? tenantId = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId ?? tenant.TenantId,
            UserId = currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Reason = reason,
            ChangesJson = changes is null ? null : AuditJson.Serialize(changes),
            IpAddress = currentUser.IpAddress,
            CorrelationId = currentUser.CorrelationId,
            CreatedAtUtc = clock.UtcNow,
        });
    }
}

internal static class AuditJson
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(object value) => System.Text.Json.JsonSerializer.Serialize(value, Options);
}
