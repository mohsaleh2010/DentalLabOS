namespace DentalLabOS.Contracts.Organization;

public sealed record BranchDto(Guid Id, string Code, string Name, string? Address, string? Phone, bool IsMain, bool IsActive, string RowVersion);

public sealed record BranchUpsertRequest(string Code, string Name, string? Address, string? Phone, bool IsActive = true, string? RowVersion = null);

public sealed record DepartmentDto(Guid Id, string Code, string Name, Guid? BranchId, string? BranchName, bool IsProduction, bool IsActive, string RowVersion);

public sealed record DepartmentUpsertRequest(string Code, string Name, Guid? BranchId, bool IsProduction, bool IsActive = true, string? RowVersion = null);
