using DentalLabOS.Application.Production;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Security;

namespace DentalLabOS.Api.Endpoints;

/// <summary>Orders, work orders, production and their settings (ORD, ADM-07/15/16/18, AUTH-08). Services re-check permissions and data scope.</summary>
public static class ProductionEndpoints
{
    public static RouteGroupBuilder MapProductionEndpoints(this RouteGroupBuilder api)
    {
        MapOrders(api);
        MapWorkOrders(api);
        MapProduction(api);
        MapSettings(api);
        return api;
    }

    private static void MapOrders(RouteGroupBuilder api)
    {
        var orders = api.MapGroup("/orders").WithTags("Orders").RequireAuthorization(PermissionPolicy.For(Permissions.Orders.Read));

        orders.MapGet("/", (int? page, int? pageSize, string? search, Guid? customerId, bool? open, OrderService svc, CancellationToken ct) =>
                svc.ListAsync(new PageQuery(page ?? 1, pageSize ?? 20, search, null), customerId, open ?? false, ct))
            .WithSummary("List customer orders (newest first)");

        orders.MapGet("/{id:guid}", (Guid id, OrderService svc, CancellationToken ct) => svc.GetAsync(id, ct)).WithName("GetOrder");

        orders.MapPost("/", async (CustomerOrderCreateRequest request, OrderService svc, CancellationToken ct) =>
            {
                var created = await svc.CreateAsync(request, ct);
                return TypedResults.CreatedAtRoute(created, "GetOrder", new { id = created.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Orders.Create))
            .WithSummary("Open an order for a clinic; add its work orders through intake with customerOrderId. Idempotent on clientOperationId");
    }

    private static void MapWorkOrders(RouteGroupBuilder api)
    {
        var wo = api.MapGroup("/workorders").WithTags("Work orders").RequireAuthorization(PermissionPolicy.For(Permissions.WorkOrders.Read));

        wo.MapGet("/", (int? page, int? pageSize, string? search, string? sort, string? status, Guid? customerId, Guid? orderId, bool? overdue,
                bool? incomplete, bool? open, WorkOrderService svc, CancellationToken ct) =>
                svc.ListAsync(new PageQuery(page ?? 1, pageSize ?? 20, search, sort),
                    new WorkOrderFilter(status, customerId, orderId, overdue, incomplete, open ?? false), ct))
            .WithSummary("List work orders (by due date; filters: status, customer, order, overdue, incomplete, open)");

        wo.MapGet("/{id:guid}", (Guid id, WorkOrderService svc, CancellationToken ct) => svc.GetAsync(id, ct)).WithName("GetWorkOrder");

        wo.MapGet("/by-number/{number}", async (string number, WorkOrderService svc, CancellationToken ct) =>
                await svc.FindByNumberAsync(number, ct) is { } found ? Results.Ok(found) : Results.NotFound())
            .WithSummary("Resolve a scanned work-order or order number (ORD-27)");

        wo.MapGet("/intake/customers", (string? search, WorkOrderService svc, CancellationToken ct) => svc.CustomerOptionsAsync(search, ct))
            .WithSummary("Customers for the intake picker");
        wo.MapGet("/intake/customers/{customerId:guid}/dentists", (Guid customerId, WorkOrderService svc, CancellationToken ct) =>
            svc.DentistOptionsAsync(customerId, ct)).WithSummary("Active dentists of a customer for the intake picker");
        wo.MapGet("/intake/services", (WorkOrderService svc, CancellationToken ct) => svc.ServiceOptionsAsync(ct))
            .WithSummary("Services with lead time and the fields their workflow requires");

        wo.MapPost("/intake", async (WorkOrderIntakeRequest request, WorkOrderService svc, CancellationToken ct) =>
            {
                var result = await svc.IntakeAsync(request, ct);
                return TypedResults.CreatedAtRoute(result, "GetWorkOrder", new { id = result.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.WorkOrders.Create))
            .WithSummary("Quick intake (ORD-05). Missing mandatory data saves it as Incomplete; warnings flag possible duplicates. Idempotent on clientOperationId");

        wo.MapPut("/{id:guid}", async (Guid id, WorkOrderUpdateRequest request, WorkOrderService svc, CancellationToken ct) =>
            {
                await svc.UpdateAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.WorkOrders.Update))
            .WithSummary("Change the prescription. After production starts a changeSummary is required and the previous version is kept");

        wo.MapGet("/{id:guid}/timeline", (Guid id, WorkOrderService svc, CancellationToken ct) => svc.TimelineAsync(id, ct));
        wo.MapGet("/{id:guid}/revisions", (Guid id, WorkOrderService svc, CancellationToken ct) => svc.RevisionsAsync(id, ct));

        wo.MapPost("/{id:guid}/notes", async (Guid id, NoteRequest request, WorkOrderService svc, CancellationToken ct) =>
        {
            await svc.AddNoteAsync(id, request, ct);
            return Results.NoContent();
        });

        wo.MapPost("/{id:guid}/attachments", async (Guid id, IFormFile file, WorkOrderService svc, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return await svc.UploadAsync(id, stream, file.FileName, ct);
            })
            .DisableAntiforgery()
            .WithSummary("Upload a photo, STL, PLY, PDF or scan archive (content is verified, 50 MB max)");

        wo.MapGet("/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, WorkOrderService svc, CancellationToken ct) =>
        {
            var file = await svc.DownloadAsync(id, attachmentId, ct);
            return TypedResults.File(file.Content, file.ContentType, file.FileName);
        });

        wo.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, WorkOrderService svc, CancellationToken ct) =>
        {
            await svc.DeleteAttachmentAsync(id, attachmentId, ct);
            return Results.NoContent();
        });

        wo.MapPost("/{id:guid}/stages/{stageId:guid}/{action}", async (Guid id, Guid stageId, string action, StageActionRequest? request,
                WorkOrderService svc, CancellationToken ct) =>
            {
                var reason = request?.Reason;
                var task = action.ToLowerInvariant() switch
                {
                    "start" => svc.StartStageAsync(id, stageId, ct),
                    "pause" => svc.PauseStageAsync(id, stageId, reason, ct),
                    "complete" => svc.CompleteStageAsync(id, stageId, ct),
                    "skip" => svc.SkipStageAsync(id, stageId, reason, ct),
                    "move-back" => svc.MoveBackAsync(id, stageId, reason, ct),
                    _ => null,
                };
                if (task is null)
                {
                    return Results.NotFound();
                }

                await task;
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.WorkOrders.Transition))
            .WithSummary("Stage action: start | pause | complete | skip (reason) | move-back (reason). Skip and move-back need production.assign");

        wo.MapPost("/{id:guid}/hold", async (Guid id, HoldRequest request, WorkOrderService svc, CancellationToken ct) =>
            {
                await svc.HoldAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.WorkOrders.Hold));

        wo.MapPost("/{id:guid}/release", async (Guid id, WorkOrderService svc, CancellationToken ct) =>
            {
                await svc.ReleaseAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.WorkOrders.Hold));

        wo.MapPost("/{id:guid}/cancel", async (Guid id, CancelRequest request, WorkOrderService svc, CancellationToken ct) =>
            {
                await svc.CancelAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.WorkOrders.Cancel));
    }

    private static void MapProduction(RouteGroupBuilder api)
    {
        var production = api.MapGroup("/production").WithTags("Production").RequireAuthorization();

        production.MapGet("/departments", (ProductionService svc, CancellationToken ct) => svc.DepartmentOptionsAsync(ct));
        production.MapGet("/board", (Guid? departmentId, Guid? workflowId, ProductionService svc, CancellationToken ct) =>
            svc.BoardAsync(departmentId, workflowId, ct));
        production.MapGet("/queue/{departmentId:guid}", (Guid departmentId, ProductionService svc, CancellationToken ct) => svc.QueueAsync(departmentId, ct));
        production.MapGet("/my-tasks", (ProductionService svc, CancellationToken ct) => svc.MyTasksAsync(ct));
        production.MapGet("/assignable", (Guid? departmentId, bool? includeAssigned, ProductionService svc, CancellationToken ct) =>
            svc.AssignableAsync(departmentId, includeAssigned ?? false, ct));
        production.MapPost("/assign", (AssignRequest request, WorkOrderService svc, CancellationToken ct) => svc.AssignAsync(request, ct))
            .WithSummary("Assign (or unassign with userId null) many stages at once");
        production.MapGet("/overdue", (ProductionService svc, CancellationToken ct) => svc.OverdueAsync(ct));
        production.MapGet("/calendar", (int year, int month, ProductionService svc, CancellationToken ct) => svc.CalendarAsync(year, month, ct));
        production.MapGet("/technicians", (Guid? departmentId, ProductionSetupService svc, CancellationToken ct) => svc.TechnicianOptionsAsync(departmentId, ct));
    }

    private static void MapSettings(RouteGroupBuilder api)
    {
        var workflows = api.MapGroup("/workflows").WithTags("Workflows").RequireAuthorization();
        workflows.MapGet("/", (WorkflowService svc, CancellationToken ct) => svc.ListAsync(ct));
        workflows.MapGet("/{id:guid}", (Guid id, WorkflowService svc, CancellationToken ct) => svc.GetAsync(id, ct)).WithName("GetWorkflow");
        workflows.MapPost("/", async (WorkflowUpsertRequest request, WorkflowService svc, CancellationToken ct) =>
            {
                var created = await svc.SaveAsync(null, request, ct);
                return TypedResults.CreatedAtRoute(created, "GetWorkflow", new { id = created.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.WorkflowsManage));
        workflows.MapPut("/{id:guid}", (Guid id, WorkflowUpsertRequest request, WorkflowService svc, CancellationToken ct) => svc.SaveAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.WorkflowsManage))
            .WithSummary("Replace a route's stages; the version goes up and only new work orders use the change");

        var reasons = api.MapGroup("/reason-codes").WithTags("Reason codes").RequireAuthorization();
        reasons.MapGet("/", (string? scope, ProductionSetupService svc, CancellationToken ct) => svc.ListReasonsAsync(scope, ct));
        reasons.MapGet("/options/{scope}", (string scope, ProductionSetupService svc, CancellationToken ct) =>
            Enum.TryParse<ReasonScope>(scope, true, out var s) ? svc.ReasonOptionsAsync(s, ct) : Task.FromResult<IReadOnlyList<ReasonCodeDto>>([]));
        reasons.MapPost("/", async (ReasonCodeUpsertRequest request, ProductionSetupService svc, CancellationToken ct) =>
            {
                await svc.SaveReasonAsync(null, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.ReasonsManage));
        reasons.MapPut("/{id:guid}", async (Guid id, ReasonCodeUpsertRequest request, ProductionSetupService svc, CancellationToken ct) =>
            {
                await svc.SaveReasonAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.ReasonsManage));

        var technicians = api.MapGroup("/technicians").WithTags("Technicians").RequireAuthorization(PermissionPolicy.For(Permissions.Admin.TechniciansManage));
        technicians.MapGet("/{userProfileId:guid}", (Guid userProfileId, ProductionSetupService svc, CancellationToken ct) => svc.GetTechnicianAsync(userProfileId, ct));
        technicians.MapPut("/{userProfileId:guid}", async (Guid userProfileId, TechnicianProfileRequest request, ProductionSetupService svc, CancellationToken ct) =>
        {
            await svc.SaveTechnicianAsync(userProfileId, request, ct);
            return Results.NoContent();
        });

        api.MapGet("/me/branches", (ProductionSetupService svc, CancellationToken ct) => svc.MyBranchesAsync(ct)).WithTags("Auth").RequireAuthorization()
            .WithSummary("Branches the caller may work in; send one as the X-Branch-Id header (AUTH-08)");
    }
}
