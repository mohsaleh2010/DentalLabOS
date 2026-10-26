namespace DentalLabOS.Domain.Common;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    Guid? CreatedByUserId { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    Guid? UpdatedByUserId { get; set; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    Guid? DeletedByUserId { get; set; }
}

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; set; } = [];
}

public abstract class TenantEntity : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>A numbered business document with a lifecycle and an idempotency key.</summary>
public abstract class DocumentEntity : TenantEntity
{
    public string Number { get; set; } = default!;
    public Guid BranchId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public Guid ClientOperationId { get; set; }
    public string? Notes { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}

/// <summary>Marks a property whose value must never be written to audit logs.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute;
