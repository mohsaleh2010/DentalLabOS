using System.Net;
using System.Net.Http.Json;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;

namespace DentalLabOS.IntegrationTests;

/// <summary>Phase 3: intake to "ready for delivery", holds, cancellation, data scope, files and isolation against the real engine.</summary>
[Collection(SqlCollection.Name)]
public sealed class ProductionTests(SqlServerFixture fx)
{
    internal static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    internal static async Task Success(HttpResponseMessage response) =>
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    internal sealed record Setup(Guid CustomerId, Guid DentistId, Guid ServiceId);

    internal static async Task<Setup> SetupAsync(HttpClient client, string name)
    {
        var customer = await Ok<CustomerDto>(await client.PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest(null, name, "Clinic")));
        var dentist = await Ok<DentistDto>(await client.PostAsJsonAsync($"/api/v1/customers/{customer.Id}/dentists", new DentistUpsertRequest("Dr. " + name)));
        var categories = await client.GetFromJsonAsync<List<ItemCategoryDto>>("/api/v1/item-categories");
        var units = await client.GetFromJsonAsync<List<UnitDto>>("/api/v1/units");
        var service = await Ok<ItemDto>(await client.PostAsJsonAsync("/api/v1/items",
            new ItemUpsertRequest(null, name + " crown", "Service", categories![0].Id, units!.First(u => u.Code == "UNIT").Id, DefaultSalesPrice: 500, StandardLeadHours: 24)));
        return new Setup(customer.Id, dentist.Id, service.Id);
    }

    internal static WorkOrderIntakeRequest Intake(Setup s, params string[] teeth) =>
        new(s.CustomerId, s.ServiceId, Guid.NewGuid(), s.DentistId, Shade: "A2", Teeth: teeth.Select(t => new ToothRequest(t)).ToList(), PatientReference: "P-1");

    [SqlFact]
    public async Task Intake_to_ready_for_delivery_follows_the_route_and_keeps_history()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Route Clinic");

        // Missing teeth: saved, but incomplete and production may not start.
        var created = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s)));
        Assert.Equal("Incomplete", created.Status);
        Assert.Contains("Teeth", created.MissingData);
        Assert.Matches(@"^WO-\d{2}-\d{6}$", created.Number);

        var wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;
        Assert.Equal(1000, 2 * wo.UnitPrice!.Value); // general price list falls back to the item's 500
        Assert.Equal(wo.Stages.Count, wo.Stages.Select(x => x.Step).Distinct().Count());
        var start = await client.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{wo.Stages[0].Id}/start", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, start.StatusCode);

        await Success(await client.PutAsJsonAsync($"/api/v1/workorders/{wo.Id}",
            new WorkOrderUpdateRequest(s.DentistId, "P-1", null, "A2", null, [new("11"), new("21")], 2, wo.DueDate, RowVersion: wo.RowVersion)));
        wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!;
        Assert.Equal("Ready", wo.Status);
        Assert.True(wo.DataComplete);

        foreach (var stage in wo.Stages)
        {
            await Success(await client.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{stage.Id}/start", null));
            if (stage.Step == 2)
            {
                // A prescription change during production needs a reason and keeps the old version.
                var current = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!;
                var noSummary = await client.PutAsJsonAsync($"/api/v1/workorders/{wo.Id}",
                    new WorkOrderUpdateRequest(s.DentistId, "P-1", null, "A3", null, [new("11"), new("21")], 2, wo.DueDate, RowVersion: current.RowVersion));
                Assert.Equal(HttpStatusCode.BadRequest, noSummary.StatusCode);
                await Success(await client.PutAsJsonAsync($"/api/v1/workorders/{wo.Id}",
                    new WorkOrderUpdateRequest(s.DentistId, "P-1", null, "A3", null, [new("11"), new("21")], 2, wo.DueDate, ChangeSummary: "Dentist changed shade",
                        RowVersion: current.RowVersion)));
            }

            await Success(await client.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{stage.Id}/complete", null));
            if (stage.RequiresQualityPass)
            {
                Assert.Equal("InQuality", (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!.Status);
                await QualityTests.AcceptAsync(client, wo.Id, stage.Id);
            }
        }

        var done = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!;
        Assert.Equal("ReadyForDelivery", done.Status);
        Assert.All(done.Stages, x => Assert.Equal("Done", x.Status));
        Assert.Equal("A3", done.Shade);

        var revisions = (await client.GetFromJsonAsync<List<RevisionDto>>($"/api/v1/workorders/{wo.Id}/revisions"))!;
        Assert.Contains(revisions, r => r.ChangeSummary == "Dentist changed shade" && r.PrescriptionJson.Contains("A2"));

        // Moving back keeps the log and reopens production.
        await Success(await client.PostAsJsonAsync($"/api/v1/workorders/{wo.Id}/stages/{done.Stages[^2].Id}/move-back", new StageActionRequest("Contacts too tight")));
        var back = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!;
        Assert.Equal("InProduction", back.Status);
        Assert.Equal(done.Stages[^2].Id, back.CurrentStageId);

        var timeline = (await client.GetFromJsonAsync<List<TimelineEntryDto>>($"/api/v1/workorders/{wo.Id}/timeline"))!;
        Assert.Contains(timeline, t => t.Action == "MovedBack" && t.Reason == "Contacts too tight");
        Assert.Equal(done.Stages.Count - 1, timeline.Count(t => t.Action == "Completed"));
        Assert.Contains(timeline, t => t.Action == "QualityAccepted");
    }

    [SqlFact]
    public async Task Hold_and_cancel_need_reasons_and_block_work()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Hold Clinic");
        var created = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "36")));
        Assert.Equal("Ready", created.Status);
        var wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;

        var holdReasons = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/hold"))!;
        await Success(await client.PostAsJsonAsync($"/api/v1/workorders/{wo.Id}/hold", new HoldRequest(holdReasons[0].Id, "Waiting for scan")));
        var blocked = await client.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{wo.Stages[0].Id}/start", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);

        await Success(await client.PostAsync($"/api/v1/workorders/{wo.Id}/release", null));
        Assert.Equal("Ready", (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!.Status);

        var cancelReasons = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/cancellation"))!;
        var wrongScope = await client.PostAsJsonAsync($"/api/v1/workorders/{wo.Id}/cancel", new CancelRequest(holdReasons[0].Id));
        Assert.Equal(HttpStatusCode.BadRequest, wrongScope.StatusCode);
        await Success(await client.PostAsJsonAsync($"/api/v1/workorders/{wo.Id}/cancel", new CancelRequest(cancelReasons[0].Id, "Clinic called")));
        var cancelled = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!;
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Contains("Clinic called", cancelled.CancelReason);
    }

    [SqlFact]
    public async Task Intake_is_idempotent_and_warns_about_duplicates()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Dup Clinic");
        var request = Intake(s, "14", "15");
        var first = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", request));
        var retry = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", request));
        Assert.Equal(first.Id, retry.Id);

        var second = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "15")));
        Assert.Contains(second.Warnings, w => w.Contains(first.Number));
    }

    [SqlFact]
    public async Task Technicians_work_only_on_their_own_or_department_stages()
    {
        var owner = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(owner, "Scope Clinic");
        var created = await Ok<WorkOrderIntakeResult>(await owner.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "46")));
        var wo = (await owner.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;
        var first = wo.Stages[0];

        var techToken = await fx.AddUserAsync(fx.LabA, SystemRoles.Technician, "scope-tech@lab-a.test");
        var tech = fx.Client(techToken);

        // No department, not assigned: refused by data scope even though the role may transition.
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{first.Id}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync("/api/v1/production/assign", new AssignRequest([first.Id], null))).StatusCode);

        var techUser = (await tech.GetFromJsonAsync<DentalLabOS.Contracts.Auth.MeResponse>("/api/v1/me"))!;
        var assigned = await Ok<AssignResultDto>(await owner.PostAsJsonAsync("/api/v1/production/assign", new AssignRequest([first.Id], techUser.UserId)));
        Assert.Equal(1, assigned.Assigned);

        var mine = (await tech.GetFromJsonAsync<MyTasksDto>("/api/v1/production/my-tasks"))!;
        Assert.Contains(mine.Mine, t => t.StageId == first.Id && t.IsAvailable);
        await Success(await tech.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{first.Id}/start", null));
        await Success(await tech.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{first.Id}/complete", null));

        // Skipping is a supervisor action.
        var next = (await owner.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!.Stages[1];
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync($"/api/v1/workorders/{wo.Id}/stages/{next.Id}/skip", new StageActionRequest("x"))).StatusCode);
    }

    [SqlFact]
    public async Task Files_are_checked_by_content_and_complete_required_data()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "File Clinic");
        var created = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "11")));

        using var fake = new MultipartFormDataContent { { new ByteArrayContent("MZ fake exe"u8.ToArray()), "file", "scan.stl" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/v1/workorders/{created.Id}/attachments", fake)).StatusCode);

        var stl = "solid t\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid t\n"u8.ToArray();
        using var form = new MultipartFormDataContent { { new ByteArrayContent(stl), "file", "../../evil/prep.stl" } };
        var uploaded = await Ok<AttachmentDto>(await client.PostAsync($"/api/v1/workorders/{created.Id}/attachments", form));
        Assert.Equal("stl", uploaded.Kind);
        Assert.Equal("prep.stl", uploaded.FileName);

        var download = await client.GetAsync($"/api/v1/workorders/{created.Id}/attachments/{uploaded.Id}");
        Assert.Equal(stl, await download.Content.ReadAsByteArrayAsync());

        // Another lab can't fetch it.
        var other = await fx.Client(fx.LabB.Token).GetAsync($"/api/v1/workorders/{created.Id}/attachments/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [SqlFact]
    public async Task Labs_cannot_see_or_touch_each_others_work_orders()
    {
        var a = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(a, "Iso Clinic");
        var created = await Ok<WorkOrderIntakeResult>(await a.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "21")));
        var wo = (await a.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;

        var b = fx.Client(fx.LabB.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/workorders/{wo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/v1/workorders/{wo.Id}/stages/{wo.Stages[0].Id}/start", null)).StatusCode);
        var assign = await Ok<AssignResultDto>(await b.PostAsJsonAsync("/api/v1/production/assign", new AssignRequest([wo.Stages[0].Id], null)));
        Assert.Equal(0, assign.Assigned);
        var list = await b.GetFromJsonAsync<PagedResult<WorkOrderListItemDto>>($"/api/v1/workorders?search={wo.Number}");
        Assert.Empty(list!.Items);

        // The technician picker (falling back to all active users when a lab has no technician profiles) stays within the lab.
        var techs = await Ok<List<TechnicianOptionDto>>(await b.GetAsync("/api/v1/production/technicians"));
        var labATechs = await Ok<List<TechnicianOptionDto>>(await a.GetAsync("/api/v1/production/technicians"));
        Assert.NotEmpty(techs);
        Assert.Empty(techs.Select(t => t.UserId).Intersect(labATechs.Select(t => t.UserId)));

        // Lab B can't file intake against Lab A's customer or service.
        var cross = await b.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "11"));
        Assert.Equal(HttpStatusCode.BadRequest, cross.StatusCode);
    }

    [SqlFact]
    public async Task Board_calendar_and_orders_reflect_work()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Board Clinic");
        var order = await Ok<CustomerOrderDto>(await client.PostAsJsonAsync("/api/v1/orders", new CustomerOrderCreateRequest(s.CustomerId, Guid.NewGuid(), s.DentistId)));
        Assert.Matches(@"^ORD-\d{2}-\d{6}$", order.Number);
        var r1 = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "11") with { CustomerOrderId = order.Id }));
        await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s) with { CustomerOrderId = order.Id, PatientReference = "P-2" }));

        var full = (await client.GetFromJsonAsync<CustomerOrderDto>($"/api/v1/orders/{order.Id}"))!;
        Assert.Equal(2, full.WorkOrders.Count);

        var board = (await client.GetFromJsonAsync<BoardDto>("/api/v1/production/board"))!;
        Assert.Contains(board.Columns.Single(c => c.Key == "ready").Cards, c => c.Id == r1.Id);
        Assert.Contains(board.Columns.Single(c => c.Key == "incomplete").Cards, c => c.CustomerName == "Board Clinic");

        var wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{r1.Id}"))!;
        var cal = (await client.GetFromJsonAsync<CalendarDto>($"/api/v1/production/calendar?year={wo.DueDate.Year}&month={wo.DueDate.Month}"))!;
        Assert.True(cal.Days.Single(d => d.Date == wo.DueDate).Due >= 1);

        var scan = (await client.GetFromJsonAsync<ScanResultDto>($"/api/v1/workorders/by-number/{wo.Number.ToLowerInvariant()}"))!;
        Assert.Equal(wo.Id, scan.Id);
    }

    [SqlFact]
    public async Task Editing_a_route_does_not_change_running_work_orders()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Version Clinic");

        // A route of its own so the lab's shared default stays untouched for the other tests.
        var route = await Ok<WorkflowDto>(await client.PostAsJsonAsync("/api/v1/workflows",
            new WorkflowUpsertRequest("Versioned route", [new(1, "Design"), new(2, "Mill"), new(3, "Finish")], RequireShade: true)));
        var item = (await client.GetFromJsonAsync<ItemDto>($"/api/v1/items/{s.ServiceId}"))!;
        await Success(await client.PutAsJsonAsync($"/api/v1/items/{item.Id}", new ItemUpsertRequest(item.Sku, item.Name, item.Type, item.CategoryId,
            item.BaseUnitId, DefaultSalesPrice: item.DefaultSalesPrice, StandardLeadHours: item.StandardLeadHours, RowVersion: item.RowVersion,
            DefaultWorkflowId: route.Id)));

        var wo = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "31")));
        var before = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!;
        Assert.Equal(["Design", "Mill", "Finish"], before.Stages.Select(x => x.Name));

        // Drop "Design" and add "Polish" in parallel with "Finish".
        var stages = route.Stages.Skip(1).Select(x => new WorkflowStageRequest(x.Step, x.Name, x.DepartmentId, x.TargetHours, x.RequiresQualityPass, x.Id))
            .Append(new WorkflowStageRequest(route.Stages.Max(x => x.Step), "Polish", null, 1)).ToList();
        var saved = await Ok<WorkflowDto>(await client.PutAsJsonAsync($"/api/v1/workflows/{route.Id}",
            new WorkflowUpsertRequest(route.Name, stages, RequireShade: true, RowVersion: route.RowVersion)));
        Assert.Equal(route.Version + 1, saved.Version);
        Assert.Equal(3, saved.Stages.Count);

        var after = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{wo.Id}"))!;
        Assert.Equal(before.Stages.Select(x => x.Name), after.Stages.Select(x => x.Name));

        // Parallel stages share a step and both must finish before the work order is ready.
        var created = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, "32")));
        var fresh = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{created.Id}"))!;
        var last = fresh.Stages.Max(y => y.Step);
        Assert.Equal(2, fresh.Stages.Count(x => x.Step == last));
        foreach (var stage in fresh.Stages.OrderBy(x => x.Step))
        {
            await Success(await client.PostAsync($"/api/v1/workorders/{fresh.Id}/stages/{stage.Id}/start", null));
            await Success(await client.PostAsync($"/api/v1/workorders/{fresh.Id}/stages/{stage.Id}/complete", null));
            var now = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{fresh.Id}"))!;
            Assert.Equal(stage.Name == fresh.Stages.OrderBy(x => x.Step).Last().Name ? "ReadyForDelivery" : "InProduction", now.Status);
        }
    }
}
