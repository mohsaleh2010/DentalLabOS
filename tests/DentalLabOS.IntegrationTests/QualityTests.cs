using System.Net;
using System.Net.Http.Json;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using static DentalLabOS.IntegrationTests.ProductionTests;

namespace DentalLabOS.IntegrationTests;

/// <summary>Phase 4: inspection, adjustments and remakes against the real engine.</summary>
[Collection(SqlCollection.Name)]
public sealed class QualityTests(SqlServerFixture fx)
{
    /// <summary>Works the order through its stages until one waits for quality; returns that stage.</summary>
    internal static async Task<Guid> RunToQualityAsync(HttpClient client, Guid workOrderId)
    {
        while (true)
        {
            var wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{workOrderId}"))!;
            var waiting = wo.Stages.FirstOrDefault(s => s.Status == "AwaitingQuality");
            if (waiting is not null)
            {
                return waiting.Id;
            }

            var next = wo.Stages.First(s => s.IsAvailable && s.Status is "Pending" or "InProgress" or "Paused");
            if (next.Status == "Pending")
            {
                await Success(await client.PostAsync($"/api/v1/workorders/{workOrderId}/stages/{next.Id}/start", null));
            }

            await Success(await client.PostAsync($"/api/v1/workorders/{workOrderId}/stages/{next.Id}/complete", null));
        }
    }

    /// <summary>Works the order through every stage, accepting each quality check, until it is ready for delivery.</summary>
    internal static async Task RunToReadyAsync(HttpClient client, Guid workOrderId)
    {
        while ((await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{workOrderId}"))!.Status != "ReadyForDelivery")
        {
            var wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{workOrderId}"))!;
            if (wo.Stages.Any(s => s.Status == "AwaitingQuality") || wo.Stages.Any(s => s.RequiresQualityPass && s.Status != "Done"))
            {
                await AcceptAsync(client, workOrderId, await RunToQualityAsync(client, workOrderId));
                continue;
            }

            var next = wo.Stages.First(s => s.IsAvailable && s.Status is "Pending" or "InProgress" or "Paused");
            if (next.Status == "Pending")
            {
                await Success(await client.PostAsync($"/api/v1/workorders/{workOrderId}/stages/{next.Id}/start", null));
            }

            await Success(await client.PostAsync($"/api/v1/workorders/{workOrderId}/stages/{next.Id}/complete", null));
        }
    }

    internal static async Task AcceptAsync(HttpClient client, Guid workOrderId, Guid stageId)
    {
        var inspection = await Ok<QualityInspectionDto>(await client.GetAsync($"/api/v1/quality/inspect/{workOrderId}?stageId={stageId}"));
        await Ok<QualityCheckResultDto>(await client.PostAsJsonAsync($"/api/v1/quality/inspect/{workOrderId}",
            new QualityCheckRequest(stageId, "Accepted", inspection.Items.Select(i => new QualityCheckItemRequest(i.Id, true)).ToList())));
    }

    [SqlFact]
    public async Task Inspection_accepts_rejects_and_requests_adjustments()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "QC Clinic");
        var created = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "11")));
        var stageId = await RunToQualityAsync(client, created.Id);

        // The stage left production and waits in the quality queue.
        var queue = (await client.GetFromJsonAsync<List<QualityQueueItemDto>>("/api/v1/quality/queue"))!;
        Assert.Contains(queue, q => q.StageId == stageId && q.WorkOrderId == created.Id);
        var inspection = await Ok<QualityInspectionDto>(await client.GetAsync($"/api/v1/quality/inspect/{created.Id}"));
        Assert.Equal(stageId, inspection.StageId);
        Assert.NotEmpty(inspection.Items);
        var critical = inspection.Items.First(i => i.IsCritical);

        // Unanswered items and a failed critical item block acceptance.
        var partial = await client.PostAsJsonAsync($"/api/v1/quality/inspect/{created.Id}",
            new QualityCheckRequest(stageId, "Accepted", [new(inspection.Items[0].Id, true)]));
        Assert.Equal(HttpStatusCode.BadRequest, partial.StatusCode);
        var failedCritical = inspection.Items.Select(i => new QualityCheckItemRequest(i.Id, i.Id != critical.Id, i.Id == critical.Id ? "Margin open" : null)).ToList();
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync($"/api/v1/quality/inspect/{created.Id}", new QualityCheckRequest(stageId, "Accepted", failedCritical))).StatusCode);

        // A rejection needs a reason and a stage to return to.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/v1/quality/inspect/{created.Id}", new QualityCheckRequest(stageId, "Rejected", failedCritical))).StatusCode);
        var rejections = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/rejection"))!;
        var returnTo = inspection.ReturnTargets[^1];
        var rejected = await Ok<QualityCheckResultDto>(await client.PostAsJsonAsync($"/api/v1/quality/inspect/{created.Id}",
            new QualityCheckRequest(stageId, "Rejected", failedCritical, rejections[0].Id, returnTo.Id, "Open margin on 11")));
        Assert.Equal("InProduction", rejected.WorkOrderStatus);
        Assert.Null(rejected.AdjustmentId);
        var reopened = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;
        Assert.Equal(returnTo.Id, reopened.CurrentStageId);

        // Second round: an adjustment request is listed under adjustments until the order passes.
        stageId = await RunToQualityAsync(client, created.Id);
        inspection = await Ok<QualityInspectionDto>(await client.GetAsync($"/api/v1/quality/inspect/{created.Id}"));
        var adjustments = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/adjustment"))!;
        var adjusted = await Ok<QualityCheckResultDto>(await client.PostAsJsonAsync($"/api/v1/quality/inspect/{created.Id}",
            new QualityCheckRequest(stageId, "AdjustmentRequested", inspection.Items.Select(i => new QualityCheckItemRequest(i.Id, true)).ToList(),
                adjustments[0].Id, stageId)));
        Assert.NotNull(adjusted.AdjustmentId);
        var open = (await client.GetFromJsonAsync<List<AdjustmentDto>>($"/api/v1/quality/adjustments?workOrderId={created.Id}"))!;
        Assert.Contains(open, a => a.Id == adjusted.AdjustmentId && a.Source == "Quality" && a.Status == "Open");

        await RunToReadyAsync(client, created.Id);
        var closed = (await client.GetFromJsonAsync<List<AdjustmentDto>>($"/api/v1/quality/adjustments?open=false&workOrderId={created.Id}"))!;
        Assert.Contains(closed, a => a.Id == adjusted.AdjustmentId && a.Status == "Closed");

        var history = (await client.GetFromJsonAsync<List<QualityCheckDto>>($"/api/v1/workorders/{created.Id}/quality"))!;
        Assert.Equal(["Accepted", "AdjustmentRequested", "Rejected"], history.Select(h => h.Result));
        Assert.Contains(history.Single(h => h.Result == "Rejected").Items, i => i.Text == critical.Text && !i.Passed && i.Note == "Margin open");
        var wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;
        Assert.Equal(3, wo.QualityChecks);
        Assert.Equal(0, wo.OpenAdjustments);
    }

    [SqlFact]
    public async Task Only_inspectors_decide_and_checklists_are_admin_managed()
    {
        var owner = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(owner, "QC Perm Clinic");
        var created = await Ok<WorkOrderIntakeResult>(await owner.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "12")));
        var stageId = await RunToQualityAsync(owner, created.Id);

        var tech = fx.Client(await fx.AddUserAsync(fx.LabA, SystemRoles.Technician, "qc-tech@lab-a.test"));
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync($"/api/v1/quality/inspect/{created.Id}",
            new QualityCheckRequest(stageId, "Accepted", []))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync("/api/v1/quality/templates",
            new QualityTemplateUpsertRequest("Tech list", [new("x")]))).StatusCode);

        var inspector = fx.Client(await fx.AddUserAsync(fx.LabA, SystemRoles.Quality, "qc@lab-a.test"));
        await AcceptAsync(inspector, created.Id, stageId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await inspector.PostAsJsonAsync($"/api/v1/quality/inspect/{created.Id}",
            new QualityCheckRequest(stageId, "Accepted", []))).StatusCode);

        // A stage-specific checklist replaces the default one for that stage.
        var template = await Ok<QualityTemplateDto>(await owner.PostAsJsonAsync("/api/v1/quality/templates",
            new QualityTemplateUpsertRequest("Zirconia final", [new("Shade matches", true), new("Contacts")])));
        Assert.Equal(["Shade matches", "Contacts"], template.Items.Select(i => i.Text));
        var route = await Ok<WorkflowDto>(await owner.PostAsJsonAsync("/api/v1/workflows",
            new WorkflowUpsertRequest("QC route", [new(1, "Mill"), new(2, "Final check", null, 1, true) { QualityTemplateId = template.Id }])));
        Assert.Equal(template.Id, route.Stages[1].QualityTemplateId);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/v1/quality/templates/{template.Id}",
            new QualityTemplateUpsertRequest(template.Name, [new("Shade matches", true)], IsActive: false, RowVersion: template.RowVersion))).StatusCode);
    }

    [SqlFact]
    public async Task Remakes_link_to_the_original_and_customer_adjustments_follow_delivery()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Remake Clinic");
        var created = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "21", "22")));
        var original = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;
        await RunToQualityAsync(client, created.Id);

        // Not ready yet: no customer adjustment before delivery.
        var adjustReasons = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/adjustment"))!;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync($"/api/v1/workorders/{created.Id}/adjustments",
            new CustomerAdjustmentRequest(adjustReasons[0].Id, Guid.NewGuid()))).StatusCode);

        // A remake at the lab's cost: the original is closed as rejected and the copy is free.
        var remakeReasons = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/remake"))!;
        var request = new RemakeRequest(created.Id, remakeReasons[0].Id, "Lab", Guid.NewGuid(), "Our milling fault");
        var remake = await Ok<LinkedWorkOrderResultDto>(await client.PostAsJsonAsync("/api/v1/quality/remakes", request));
        var retry = await Ok<LinkedWorkOrderResultDto>(await client.PostAsJsonAsync("/api/v1/quality/remakes", request));
        Assert.Equal(remake.WorkOrderId, retry.WorkOrderId);

        var closed = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;
        Assert.Equal("Rejected", closed.Status);
        Assert.Equal(1, closed.Remakes);
        var copy = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{remake.WorkOrderId}"))!;
        Assert.Equal("Remake", copy.Kind);
        Assert.Equal(created.Id, copy.OriginalWorkOrderId);
        Assert.Equal(original.Number, copy.OriginalNumber);
        Assert.Equal(original.Teeth.Select(t => t.Code), copy.Teeth.Select(t => t.Code));
        Assert.Equal(0, copy.UnitPrice);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync("/api/v1/quality/remakes", request with { ClientOperationId = Guid.NewGuid() })).StatusCode);

        var list = (await client.GetFromJsonAsync<List<RemakeListItemDto>>($"/api/v1/quality/remakes?workOrderId={created.Id}"))!;
        Assert.Contains(list, r => r.RemakeWorkOrderId == remake.WorkOrderId && r.ChargeTo == "Lab" && r.Decision == "Our milling fault");

        // Deliver the remake, then the clinic returns it for a touch-up: a linked adjustment order is opened.
        await DeliveryTests.DeliverAsync(client, s.CustomerId, remake.WorkOrderId);
        var adjustment = await Ok<LinkedWorkOrderResultDto>(await client.PostAsJsonAsync($"/api/v1/workorders/{remake.WorkOrderId}/adjustments",
            new CustomerAdjustmentRequest(adjustReasons[0].Id, Guid.NewGuid(), "Shade slightly light")));
        var fix = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{adjustment.WorkOrderId}"))!;
        Assert.Equal("Adjustment", fix.Kind);
        Assert.Equal(remake.WorkOrderId, fix.OriginalWorkOrderId);
        var open = (await client.GetFromJsonAsync<List<AdjustmentDto>>($"/api/v1/quality/adjustments?workOrderId={remake.WorkOrderId}"))!;
        Assert.Contains(open, a => a.Source == "Customer" && a.AdjustmentWorkOrderId == fix.Id);

        // Another lab sees none of it.
        var b = fx.Client(fx.LabB.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/quality/inspect/{created.Id}")).StatusCode);
        Assert.DoesNotContain((await b.GetFromJsonAsync<List<RemakeListItemDto>>("/api/v1/quality/remakes"))!, r => r.OriginalWorkOrderId == created.Id);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await b.PostAsJsonAsync("/api/v1/quality/remakes", request with { ClientOperationId = Guid.NewGuid() })).StatusCode);
    }
}
