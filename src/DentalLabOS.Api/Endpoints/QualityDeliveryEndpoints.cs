using DentalLabOS.Application.Production;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Security;

namespace DentalLabOS.Api.Endpoints;

/// <summary>Quality, remakes and delivery (ORD-17..24, ADM-17). Services re-check permissions.</summary>
public static class QualityDeliveryEndpoints
{
    public static RouteGroupBuilder MapQualityDeliveryEndpoints(this RouteGroupBuilder api)
    {
        MapQuality(api);
        MapDelivery(api);
        return api;
    }

    private static void MapQuality(RouteGroupBuilder api)
    {
        var templates = api.MapGroup("/quality/templates").WithTags("Quality").RequireAuthorization();
        templates.MapGet("/", (QualityService svc, CancellationToken ct) => svc.ListTemplatesAsync(ct));
        templates.MapGet("/{id:guid}", (Guid id, QualityService svc, CancellationToken ct) => svc.GetTemplateAsync(id, ct)).WithName("GetQualityTemplate");
        templates.MapPost("/", async (QualityTemplateUpsertRequest request, QualityService svc, CancellationToken ct) =>
            {
                var created = await svc.SaveTemplateAsync(null, request, ct);
                return TypedResults.CreatedAtRoute(created, "GetQualityTemplate", new { id = created.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.QualityManage));
        templates.MapPut("/{id:guid}", (Guid id, QualityTemplateUpsertRequest request, QualityService svc, CancellationToken ct) => svc.SaveTemplateAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.QualityManage))
            .WithSummary("Replace a checklist's items; past inspections keep the texts they were checked against");

        var quality = api.MapGroup("/quality").WithTags("Quality").RequireAuthorization();
        quality.MapGet("/overview", (QualityService svc, CancellationToken ct) => svc.OverviewAsync(ct));
        quality.MapGet("/queue", (Guid? departmentId, QualityService svc, CancellationToken ct) => svc.QueueAsync(departmentId, ct))
            .WithSummary("Stages waiting for inspection, earliest due first");
        quality.MapGet("/checks", (string? result, QualityService svc, CancellationToken ct) => svc.RecentChecksAsync(result, ct));
        quality.MapGet("/inspect/{workOrderId:guid}", (Guid workOrderId, Guid? stageId, QualityService svc, CancellationToken ct) =>
            svc.InspectionAsync(workOrderId, stageId, ct));
        quality.MapPost("/inspect/{workOrderId:guid}", (Guid workOrderId, QualityCheckRequest request, QualityService svc, CancellationToken ct) =>
                svc.InspectAsync(workOrderId, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Quality.Inspect))
            .WithSummary("Accept, reject or ask for an adjustment. Rejections and adjustments need a reason and the stage to return to");
        quality.MapGet("/adjustments", (bool? open, Guid? workOrderId, QualityService svc, CancellationToken ct) =>
            svc.AdjustmentsAsync(open ?? true, workOrderId, ct));
        quality.MapGet("/remakes", (Guid? workOrderId, QualityService svc, CancellationToken ct) => svc.RemakesAsync(workOrderId, ct));
        quality.MapPost("/remakes", (RemakeRequest request, QualityService svc, CancellationToken ct) => svc.CreateRemakeAsync(request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Quality.RemakeManage))
            .WithSummary("Open a remake linked to the original; who bears the cost is recorded as a decision");

        api.MapGet("/workorders/{id:guid}/quality", (Guid id, QualityService svc, CancellationToken ct) => svc.HistoryAsync(id, ct))
            .WithTags("Work orders").RequireAuthorization();
        api.MapPost("/workorders/{id:guid}/adjustments", (Guid id, CustomerAdjustmentRequest request, QualityService svc, CancellationToken ct) =>
                svc.RequestCustomerAdjustmentAsync(id, request, ct))
            .WithTags("Work orders").RequireAuthorization(PermissionPolicy.For(Permissions.Quality.RemakeManage))
            .WithSummary("The clinic returned a delivered case for correction: opens a linked adjustment order");
    }

    private static void MapDelivery(RouteGroupBuilder api)
    {
        var delivery = api.MapGroup("/delivery").WithTags("Delivery").RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Read));
        delivery.MapGet("/ready", (string? search, DeliveryService svc, CancellationToken ct) => svc.ReadyAsync(search, ct))
            .WithSummary("Orders that passed quality and are not on a delivery note, grouped by clinic");

        delivery.MapGet("/notes", (int? page, int? pageSize, string? search, string? status, Guid? runId, DeliveryService svc, CancellationToken ct) =>
            svc.ListNotesAsync(new PageQuery(page ?? 1, pageSize ?? 20, search, null), status, runId, ct));
        delivery.MapGet("/notes/{id:guid}", (Guid id, DeliveryService svc, CancellationToken ct) => svc.GetNoteAsync(id, ct)).WithName("GetDeliveryNote");
        delivery.MapPost("/notes", async (DeliveryNoteCreateRequest request, DeliveryService svc, CancellationToken ct) =>
            {
                var note = await svc.CreateNoteAsync(request, ct);
                return TypedResults.CreatedAtRoute(note, "GetDeliveryNote", new { id = note.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage));
        delivery.MapPost("/notes/{id:guid}/packaging", async (Guid id, PackagingCheckRequest request, DeliveryService svc, CancellationToken ct) =>
            {
                await svc.CheckPackagingAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage));
        delivery.MapDelete("/notes/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, DeliveryService svc, CancellationToken ct) =>
            {
                await svc.RemoveLineAsync(id, lineId, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage));
        delivery.MapPost("/notes/{id:guid}/dispatch", async (Guid id, DeliveryService svc, CancellationToken ct) =>
            {
                await svc.DispatchAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage))
            .WithSummary("The note leaves the lab; every line's packaging must be checked");
        delivery.MapPost("/notes/{id:guid}/cancel", async (Guid id, DeliveryService svc, CancellationToken ct) =>
            {
                await svc.CancelNoteAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage));
        delivery.MapPost("/notes/{id:guid}/confirm", async (Guid id, DeliveryConfirmRequest request, DeliveryService svc, CancellationToken ct) =>
            {
                await svc.ConfirmAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Confirm))
            .WithSummary("Record who received it, or why delivery failed (the orders return to ready)");
        delivery.MapPost("/notes/{id:guid}/proof", async (Guid id, IFormFile file, DeliveryService svc, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                await svc.UploadProofAsync(id, stream, file.FileName, ct);
                return Results.NoContent();
            })
            .DisableAntiforgery()
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Confirm))
            .WithSummary("Photo or PDF of the signed note (content is verified)");
        delivery.MapGet("/notes/{id:guid}/proof", async (Guid id, DeliveryService svc, CancellationToken ct) =>
        {
            var file = await svc.DownloadProofAsync(id, ct);
            return TypedResults.File(file.Content, file.ContentType, file.FileName);
        });

        delivery.MapGet("/runs", (DateOnly? from, DeliveryService svc, CancellationToken ct) => svc.ListRunsAsync(from, ct));
        delivery.MapGet("/runs/{id:guid}", (Guid id, DeliveryService svc, CancellationToken ct) => svc.GetRunAsync(id, ct)).WithName("GetDeliveryRun");
        delivery.MapPost("/runs", async (DeliveryRunRequest request, DeliveryService svc, CancellationToken ct) =>
            {
                var id = await svc.SaveRunAsync(null, request, ct);
                return TypedResults.CreatedAtRoute(await svc.GetRunAsync(id, ct), "GetDeliveryRun", new { id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage));
        delivery.MapPut("/runs/{id:guid}", async (Guid id, DeliveryRunRequest request, DeliveryService svc, CancellationToken ct) =>
            {
                await svc.SaveRunAsync(id, request, ct);
                return await svc.GetRunAsync(id, ct);
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage));
        delivery.MapPut("/runs/{id:guid}/stops", async (Guid id, RunStopsRequest request, DeliveryService svc, CancellationToken ct) =>
            {
                await svc.SetStopsAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage))
            .WithSummary("Set the stops in visiting order");
        delivery.MapPost("/runs/{id:guid}/{action}", async (Guid id, string action, DeliveryService svc, CancellationToken ct) =>
            {
                var task = action.ToLowerInvariant() switch
                {
                    "start" => svc.StartRunAsync(id, ct),
                    "complete" => svc.CompleteRunAsync(id, ct),
                    _ => null,
                };
                if (task is null)
                {
                    return Results.NotFound();
                }

                await task;
                return Results.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Delivery.Manage))
            .WithSummary("start (dispatches the run's draft notes) | complete (every stop delivered or failed)");
    }
}
