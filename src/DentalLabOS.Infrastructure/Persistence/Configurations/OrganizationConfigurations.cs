using DentalLabOS.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalLabOS.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.LaborRatePerHour).HasPrecision(18, 4);
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.JobTitle).HasMaxLength(100);
        b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        b.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.BranchScopes).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.DepartmentScopes).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TechnicianProfileConfiguration : IEntityTypeConfiguration<TechnicianProfile>
{
    public void Configure(EntityTypeBuilder<TechnicianProfile> b)
    {
        b.Property(x => x.Skills).HasMaxLength(1000);
        b.HasIndex(x => new { x.TenantId, x.UserProfileId }).IsUnique();
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.SystemKey).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.Module).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200);
        b.HasIndex(x => x.Key).IsUnique();
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.RoleId, x.PermissionId }).IsUnique();
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.UserProfileId, x.RoleId }).IsUnique();
    }
}

internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> b)
    {
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Email });
    }
}

internal sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> b)
    {
        b.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Prefix).HasMaxLength(10).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.DocumentType, x.BranchId, x.Year }).IsUnique();
    }
}

internal sealed class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicy> b)
    {
        b.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Trigger).HasMaxLength(50).IsRequired();
        b.Property(x => x.Threshold).HasPrecision(18, 2);
    }
}

internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> b)
    {
        b.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId });
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}
