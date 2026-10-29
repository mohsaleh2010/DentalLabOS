using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalLabOS.Infrastructure.Persistence.Configurations;

internal sealed class CustomerOrderConfiguration : IEntityTypeConfiguration<CustomerOrder>
{
    public void Configure(EntityTypeBuilder<CustomerOrder> b)
    {
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.VoidReason).HasMaxLength(500);
        b.Property(x => x.DeliveryInstructions).HasMaxLength(1000);
        b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.ClientOperationId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CustomerId, x.DocumentDate });
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Dentist>().WithMany().HasForeignKey(x => x.DentistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.WorkOrders).WithOne().HasForeignKey(x => x.CustomerOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> b)
    {
        b.Ignore(x => x.IsOpen);
        b.Ignore(x => x.ProductionStarted);
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.Property(x => x.PriorityReason).HasMaxLength(300);
        b.Property(x => x.PatientReference).HasMaxLength(100);
        b.Property(x => x.MaterialName).HasMaxLength(150);
        b.Property(x => x.Shade).HasMaxLength(50);
        b.Property(x => x.Instructions).HasMaxLength(4000);
        b.Property(x => x.MissingDataNotes).HasMaxLength(200);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.ClientOperationId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Status, x.DueDate });
        b.HasIndex(x => new { x.TenantId, x.CustomerId, x.ReceivedAtUtc });
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Dentist>().WithMany().HasForeignKey(x => x.DentistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.OriginalWorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Teeth).WithOne().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Stages).WithOne(x => x.WorkOrder).HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Notes).WithOne().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkOrderToothConfiguration : IEntityTypeConfiguration<WorkOrderTooth>
{
    public void Configure(EntityTypeBuilder<WorkOrderTooth> b)
    {
        b.Property(x => x.ToothCode).HasMaxLength(10).IsRequired();
        b.Property(x => x.Treatment).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(300);
        b.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.ToothCode }).IsUnique();
    }
}

internal sealed class WorkOrderRevisionConfiguration : IEntityTypeConfiguration<WorkOrderRevision>
{
    public void Configure(EntityTypeBuilder<WorkOrderRevision> b)
    {
        b.Property(x => x.PrescriptionJson).HasMaxLength(-1).IsRequired();
        b.Property(x => x.ChangeSummary).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.RevisionNumber }).IsUnique();
    }
}

internal sealed class WorkOrderAttachmentConfiguration : IEntityTypeConfiguration<WorkOrderAttachment>
{
    public void Configure(EntityTypeBuilder<WorkOrderAttachment> b)
    {
        b.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkOrderNoteConfiguration : IEntityTypeConfiguration<WorkOrderNote>
{
    public void Configure(EntityTypeBuilder<WorkOrderNote> b) => b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
}

internal sealed class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
{
    public void Configure(EntityTypeBuilder<FileMetadata> b)
    {
        b.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.ScanStatus).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.StorageKey).IsUnique();
    }
}

internal sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsDefault] = 1").HasDatabaseName("IX_Workflows_OneDefaultPerTenant");
        b.HasMany(x => x.Stages).WithOne().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkflowStageDefinitionConfiguration : IEntityTypeConfiguration<WorkflowStageDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowStageDefinition> b)
    {
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.TargetHours).HasPrecision(9, 2);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.WorkflowDefinitionId, x.Step });
    }
}

internal sealed class WorkOrderStageConfiguration : IEntityTypeConfiguration<WorkOrderStage>
{
    public void Configure(EntityTypeBuilder<WorkOrderStage> b)
    {
        b.Ignore(x => x.IsFinished);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.SkipReason).HasMaxLength(500);
        b.Property(x => x.TargetHours).HasPrecision(9, 2);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        // The stage keeps its own copy; the template row may change or go away (the designer clears the link first).
        b.HasOne<WorkflowStageDefinition>().WithMany().HasForeignKey(x => x.StageDefinitionId).OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.TimeEntries).WithOne().HasForeignKey(x => x.WorkOrderStageId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Status, x.DepartmentId });
        b.HasIndex(x => new { x.TenantId, x.AssignedUserId, x.Status });
    }
}

internal sealed class StageTransitionLogConfiguration : IEntityTypeConfiguration<StageTransitionLog>
{
    public void Configure(EntityTypeBuilder<StageTransitionLog> b)
    {
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.AtUtc });
    }
}

internal sealed class TaskTimeEntryConfiguration : IEntityTypeConfiguration<TaskTimeEntry>
{
    public void Configure(EntityTypeBuilder<TaskTimeEntry> b)
    {
        b.Property(x => x.LaborRateSnapshot).HasPrecision(18, 4);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.EndedAtUtc });
        b.HasIndex(x => new { x.TenantId, x.WorkOrderId });
    }
}

internal sealed class HoldRecordConfiguration : IEntityTypeConfiguration<HoldRecord>
{
    public void Configure(EntityTypeBuilder<HoldRecord> b)
    {
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ReasonCode>().WithMany().HasForeignKey(x => x.ReasonCodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.ResumedAtUtc });
    }
}

internal sealed class ReasonCodeConfiguration : IEntityTypeConfiguration<ReasonCode>
{
    public void Configure(EntityTypeBuilder<ReasonCode> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Scope, x.Name }).IsUnique();
    }
}
