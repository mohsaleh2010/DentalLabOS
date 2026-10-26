using System.Reflection;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DentalLabOS.Infrastructure.Persistence;

/// <summary>Raised when code tries to write a row that belongs to a different tenant than the current context.</summary>
public sealed class TenantIsolationException(string message) : InvalidOperationException(message);

/// <summary>
/// Central write-side guard (TENANT_ISOLATION.md):
/// stamps TenantId and audit columns, refuses cross-tenant writes, turns deletes of soft-deletable rows into flags,
/// and records an insert-only audit trail of changes (sensitive values masked).
/// </summary>
public sealed class TenantSaveChangesInterceptor(ICurrentUser currentUser, IClock clock) : SaveChangesInterceptor
{
    // Join rows audited at the service level with business context (PermissionChange/RoleAssignment).
    private static readonly HashSet<Type> SkipAutoAudit = [typeof(RolePermission), typeof(UserRole)];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is not AppDbContext db)
        {
            return;
        }

        var tenantId = db.TenantContext.TenantId;
        var now = clock.UtcNow;
        var userId = currentUser.UserId;
        var audits = new List<AuditLog>();

        foreach (var entry in db.ChangeTracker.Entries<ITenantOwned>().ToList())
        {
            GuardTenant(entry, tenantId);

            if (entry.Entity is ISoftDeletable soft && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAtUtc = now;
                soft.DeletedByUserId = userId;
            }

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedByUserId = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAtUtc = now;
                    auditable.UpdatedByUserId = userId;
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedByUserId)).IsModified = false;
                }
            }

            if (!SkipAutoAudit.Contains(entry.Metadata.ClrType) && BuildAudit(entry, userId, now) is { } log)
            {
                audits.Add(log);
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit log rows are insert-only.");
            }
        }

        db.AuditLogs.AddRange(audits);
    }

    private static void GuardTenant(EntityEntry<ITenantOwned> entry, Guid? tenantId)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                if (tenantId is null)
                {
                    throw new TenantIsolationException($"Cannot create {entry.Metadata.ClrType.Name} without a tenant context.");
                }

                if (entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = tenantId.Value;
                }
                else if (entry.Entity.TenantId != tenantId)
                {
                    throw new TenantIsolationException($"Cross-tenant insert of {entry.Metadata.ClrType.Name} was blocked.");
                }

                break;

            case EntityState.Modified:
            case EntityState.Deleted:
                var original = (Guid)entry.Property(nameof(ITenantOwned.TenantId)).OriginalValue!;
                if (tenantId is null || original != tenantId || entry.Entity.TenantId != tenantId)
                {
                    throw new TenantIsolationException($"Cross-tenant change of {entry.Metadata.ClrType.Name} was blocked.");
                }

                break;
        }
    }

    private AuditLog? BuildAudit(EntityEntry<ITenantOwned> entry, Guid? userId, DateTime now)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditActions.Create,
            EntityState.Modified => AuditActions.Update,
            EntityState.Deleted => AuditActions.Delete,
            _ => null,
        };
        if (action is null)
        {
            return null;
        }

        var changes = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (name is nameof(Entity.RowVersion) or nameof(ITenantOwned.TenantId) or nameof(Entity.Id)
                || name.EndsWith("AtUtc", StringComparison.Ordinal) || name.EndsWith("ByUserId", StringComparison.Ordinal))
            {
                continue;
            }

            var sensitive = p.Metadata.PropertyInfo?.GetCustomAttribute<SensitiveDataAttribute>() is not null;
            if (entry.State == EntityState.Added)
            {
                changes[name] = sensitive ? "***" : p.CurrentValue;
            }
            else if (entry.State == EntityState.Modified && p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
            {
                changes[name] = sensitive ? new { from = "***", to = "***" } : new { from = p.OriginalValue, to = p.CurrentValue };
            }
        }

        if (entry.State == EntityState.Modified && changes.Count == 0)
        {
            return null;
        }

        return new AuditLog
        {
            TenantId = entry.Entity.TenantId,
            UserId = userId,
            Action = action,
            EntityType = entry.Metadata.ClrType.Name,
            EntityId = (entry.Entity as Entity)?.Id,
            ChangesJson = changes.Count == 0 ? null : AuditJson.Serialize(changes),
            IpAddress = currentUser.IpAddress,
            CorrelationId = currentUser.CorrelationId,
            CreatedAtUtc = now,
        };
    }
}
