using System.Net;
using System.Net.Http.Json;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using static DentalLabOS.IntegrationTests.ProductionTests;

namespace DentalLabOS.IntegrationTests;

/// <summary>Phase 4: delivery notes, packaging checks, confirmation and runs.</summary>
[Collection(SqlCollection.Name)]
public sealed class DeliveryTests(SqlServerFixture fx)
{
    /// <summary>Puts a ready order on a note, checks the packaging and confirms receipt.</summary>
    internal static async Task DeliverAsync(HttpClient client, Guid customerId, Guid workOrderId)
    {
        await QualityTests.RunToReadyAsync(client, workOrderId);
        var note = await Ok<DeliveryNoteDto>(await client.PostAsJsonAsync("/api/v1/delivery/notes",
            new DeliveryNoteCreateRequest(customerId, [workOrderId], Guid.NewGuid())));
        await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{note.Id}/packaging", new PackagingCheckRequest(note.Lines.Select(l => l.Id).ToList())));
        await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{note.Id}/confirm", new DeliveryConfirmRequest(true, "Front desk")));
    }

    private static async Task<Guid> ReadyOrderAsync(HttpClient client, Setup s, string tooth)
    {
        var created = await Ok<WorkOrderIntakeResult>(await client.PostAsJsonAsync("/api/v1/workorders/intake", Intake(s, tooth)));
        await QualityTests.RunToReadyAsync(client, created.Id);
        return created.Id;
    }

    [SqlFact]
    public async Task Delivery_notes_need_packaging_checks_and_confirmation()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Courier Clinic");
        var other = await SetupAsync(client, "Other Clinic");
        var first = await ReadyOrderAsync(client, s, "11");
        var second = await ReadyOrderAsync(client, s, "21");
        var foreign = await ReadyOrderAsync(client, other, "31");

        var ready = (await client.GetFromJsonAsync<List<ReadyGroupDto>>("/api/v1/delivery/ready"))!;
        var group = ready.Single(g => g.CustomerId == s.CustomerId).WorkOrders.Select(w => w.Id).ToList();
        Assert.Equal(2, group.Count);
        Assert.Contains(first, group);
        Assert.Contains(second, group);

        // One clinic per note.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/delivery/notes",
            new DeliveryNoteCreateRequest(s.CustomerId, [first, foreign], Guid.NewGuid()))).StatusCode);

        var request = new DeliveryNoteCreateRequest(s.CustomerId, [first, second], Guid.NewGuid(), "Fragile");
        var note = await Ok<DeliveryNoteDto>(await client.PostAsJsonAsync("/api/v1/delivery/notes", request));
        Assert.Equal(note.Id, (await Ok<DeliveryNoteDto>(await client.PostAsJsonAsync("/api/v1/delivery/notes", request))).Id);
        Assert.Matches(@"^DN-\d{2}-\d{6}$", note.Number);
        Assert.Equal("Draft", note.Status);
        Assert.Equal(2, note.Lines.Count);

        // An order can be on only one active note, and it can't go back to production while it is.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/v1/delivery/notes",
            new DeliveryNoteCreateRequest(s.CustomerId, [first], Guid.NewGuid()))).StatusCode);
        var wo = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{first}"))!;
        Assert.Equal(note.Id, wo.DeliveryNoteId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync($"/api/v1/workorders/{first}/stages/{wo.Stages[0].Id}/move-back",
            new StageActionRequest("Not right"))).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<ReadyGroupDto>>("/api/v1/delivery/ready"))!.SelectMany(g => g.WorkOrders), w => w.Id == first);

        // Packaging must be checked on every line before it leaves.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/api/v1/delivery/notes/{note.Id}/dispatch", null)).StatusCode);
        await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{note.Id}/packaging", new PackagingCheckRequest([note.Lines[0].Id])));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/api/v1/delivery/notes/{note.Id}/dispatch", null)).StatusCode);
        await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{note.Id}/packaging", new PackagingCheckRequest([note.Lines[1].Id])));
        await Success(await client.PostAsync($"/api/v1/delivery/notes/{note.Id}/dispatch", null));

        // Receipt needs who received it; then the orders are delivered.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/v1/delivery/notes/{note.Id}/confirm", new DeliveryConfirmRequest(true))).StatusCode);
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9 };
        using (var proof = new MultipartFormDataContent { { new ByteArrayContent(jpeg), "file", "signed.jpg" } })
        {
            await Success(await client.PostAsync($"/api/v1/delivery/notes/{note.Id}/proof", proof));
        }

        await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{note.Id}/confirm", new DeliveryConfirmRequest(true, "Dr. Courier's nurse")));
        var delivered = (await client.GetFromJsonAsync<DeliveryNoteDto>($"/api/v1/delivery/notes/{note.Id}"))!;
        Assert.Equal("Delivered", delivered.Status);
        Assert.Equal("Dr. Courier's nurse", delivered.ReceivedByName);
        Assert.True(delivered.HasProof);
        Assert.Equal(jpeg, await (await client.GetAsync($"/api/v1/delivery/notes/{note.Id}/proof")).Content.ReadAsByteArrayAsync());
        var done = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{first}"))!;
        Assert.Equal("Delivered", done.Status);
        Assert.NotNull(done.DeliveredAtUtc);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync($"/api/v1/delivery/notes/{note.Id}/confirm", new DeliveryConfirmRequest(true, "Again"))).StatusCode);
        var timeline = (await client.GetFromJsonAsync<List<TimelineEntryDto>>($"/api/v1/workorders/{first}/timeline"))!;
        Assert.Contains(timeline, t => t.Action == "Delivered");

        // Another lab sees nothing.
        var b = fx.Client(fx.LabB.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/delivery/notes/{note.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/delivery/notes/{note.Id}/proof")).StatusCode);
        var bNotes = (await b.GetFromJsonAsync<PagedResult<DeliveryNoteListItemDto>>($"/api/v1/delivery/notes?search={note.Number}"))!;
        Assert.Empty(bNotes.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/api/v1/delivery/notes",
            new DeliveryNoteCreateRequest(other.CustomerId, [foreign], Guid.NewGuid()))).StatusCode);
    }

    [SqlFact]
    public async Task Failed_deliveries_release_orders_and_runs_track_stops()
    {
        var client = fx.Client(fx.LabA.Token);
        var s = await SetupAsync(client, "Run Clinic");
        var a = await ReadyOrderAsync(client, s, "14");
        var b = await ReadyOrderAsync(client, s, "15");

        var noteA = await Ok<DeliveryNoteDto>(await client.PostAsJsonAsync("/api/v1/delivery/notes", new DeliveryNoteCreateRequest(s.CustomerId, [a], Guid.NewGuid())));
        var noteB = await Ok<DeliveryNoteDto>(await client.PostAsJsonAsync("/api/v1/delivery/notes", new DeliveryNoteCreateRequest(s.CustomerId, [b], Guid.NewGuid())));
        foreach (var n in new[] { noteA, noteB })
        {
            await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{n.Id}/packaging", new PackagingCheckRequest(n.Lines.Select(l => l.Id).ToList())));
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/delivery/runs", new DeliveryRunRequest(DateOnly.FromDateTime(DateTime.UtcNow)))).StatusCode);
        var run = await Ok<DeliveryRunDto>(await client.PostAsJsonAsync("/api/v1/delivery/runs",
            new DeliveryRunRequest(DateOnly.FromDateTime(DateTime.UtcNow), CourierName: "Outside driver")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/api/v1/delivery/runs/{run.Id}/start", null)).StatusCode);
        await Success(await client.PutAsJsonAsync($"/api/v1/delivery/runs/{run.Id}/stops", new RunStopsRequest([noteB.Id, noteA.Id])));
        run = (await client.GetFromJsonAsync<DeliveryRunDto>($"/api/v1/delivery/runs/{run.Id}"))!;
        Assert.Equal([noteB.Id, noteA.Id], run.Stops.Select(x => x.Id));

        await Success(await client.PostAsync($"/api/v1/delivery/runs/{run.Id}/start", null));
        Assert.Equal("OutForDelivery", (await client.GetFromJsonAsync<DeliveryNoteDto>($"/api/v1/delivery/notes/{noteA.Id}"))!.Status);

        // A failed delivery needs a delivery-failure reason and puts the order back on the ready list.
        var holdReasons = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/hold"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/delivery/notes/{noteA.Id}/confirm",
            new DeliveryConfirmRequest(false, FailureReasonCodeId: holdReasons[0].Id))).StatusCode);
        var failures = (await client.GetFromJsonAsync<List<ReasonCodeDto>>("/api/v1/reason-codes/options/deliveryfailure"))!;
        Assert.Equal(3, failures.Count);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/api/v1/delivery/runs/{run.Id}/complete", null)).StatusCode);
        await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{noteA.Id}/confirm",
            new DeliveryConfirmRequest(false, FailureReasonCodeId: failures[0].Id, FailureNotes: "Closed at 4pm")));
        await Success(await client.PostAsJsonAsync($"/api/v1/delivery/notes/{noteB.Id}/confirm", new DeliveryConfirmRequest(true, "Reception")));
        await Success(await client.PostAsync($"/api/v1/delivery/runs/{run.Id}/complete", null));

        var failed = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/workorders/{a}"))!;
        Assert.Equal("ReadyForDelivery", failed.Status);
        Assert.Null(failed.DeliveryNoteId);
        Assert.Contains((await client.GetFromJsonAsync<List<ReadyGroupDto>>("/api/v1/delivery/ready"))!.SelectMany(g => g.WorkOrders), w => w.Id == a);
        var runs = (await client.GetFromJsonAsync<List<DeliveryRunListItemDto>>("/api/v1/delivery/runs"))!;
        Assert.Contains(runs, r => r.Id == run.Id && r.Status == "Completed" && r.Delivered == 1 && r.Failed == 1);

        // Courier role confirms but does not need production rights; technicians can't touch delivery.
        var courier = fx.Client(await fx.AddUserAsync(fx.LabA, SystemRoles.Delivery, "courier@lab-a.test"));
        Assert.Equal(HttpStatusCode.OK, (await courier.GetAsync("/api/v1/delivery/ready")).StatusCode);
        var tech = fx.Client(await fx.AddUserAsync(fx.LabA, SystemRoles.Technician, "dn-tech@lab-a.test"));
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.GetAsync("/api/v1/delivery/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync("/api/v1/delivery/notes",
            new DeliveryNoteCreateRequest(s.CustomerId, [a], Guid.NewGuid()))).StatusCode);
    }
}
