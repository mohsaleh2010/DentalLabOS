using DentalLabOS.Application.Organization;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Security;

namespace DentalLabOS.Api.Endpoints;

/// <summary>Branches (ADM-03). The service re-checks permissions and the tenant filter scopes every query.</summary>
public static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this RouteGroupBuilder api)
    {
        var branches = api.MapGroup("/branches").WithTags("Branches")
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.BranchesManage));

        branches.MapGet("/", (int? page, int? pageSize, string? search, BranchService svc, CancellationToken ct) =>
                svc.ListAsync(new PageQuery(page ?? 1, pageSize ?? 20, search), ct))
            .WithSummary("List branches (paged)");

        branches.MapGet("/{id:guid}", (Guid id, BranchService svc, CancellationToken ct) => svc.GetAsync(id, ct))
            .WithName("GetBranch")
            .WithSummary("Get one branch");

        branches.MapPost("/", async (BranchUpsertRequest request, BranchService svc, CancellationToken ct) =>
            {
                var created = await svc.CreateAsync(request, ct);
                return TypedResults.CreatedAtRoute(created, "GetBranch", new { id = created.Id });
            })
            .WithSummary("Create a branch (within the plan's branch limit)");

        branches.MapPut("/{id:guid}", (Guid id, BranchUpsertRequest request, BranchService svc, CancellationToken ct) =>
                svc.UpdateAsync(id, request, ct))
            .WithSummary("Update a branch; send the RowVersion you read to detect concurrent edits");

        return api;
    }
}
