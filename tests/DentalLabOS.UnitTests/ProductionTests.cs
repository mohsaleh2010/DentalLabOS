using System.Text;
using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Production;

namespace DentalLabOS.UnitTests;

public sealed class WorkflowEngineTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Design → (Mill ∥ Print) → Finish.</summary>
    private static WorkOrder NewOrder(bool complete = true)
    {
        var wf = new WorkflowDefinition { Id = Guid.NewGuid(), Name = "Route", Version = 3, RequireTeeth = true, RequireDentist = false };
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 1, Name = "Design" });
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 2, Name = "Print" });
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 2, Name = "Mill" });
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 3, Name = "Finish" });

        var wo = new WorkOrder { Id = Guid.NewGuid(), Number = "WO-26-000001" };
        if (complete)
        {
            wo.Teeth.Add(new WorkOrderTooth { ToothCode = "11" });
        }

        WorkflowEngine.CopyStages(wo, wf);
        WorkflowEngine.ApplyDataCheck(wo, WorkflowEngine.MissingData(wf, wo, 0), User, Now);
        return wo;
    }

    private static WorkOrderStage Stage(WorkOrder wo, string name) => wo.Stages.Single(s => s.Name == name);

    private static void Finish(WorkOrder wo, string name)
    {
        WorkflowEngine.Start(wo, Stage(wo, name), User, Now);
        WorkflowEngine.Complete(wo, Stage(wo, name), User, Now);
    }

    [Fact]
    public void Copied_stages_are_a_snapshot_of_the_route_version()
    {
        var wo = NewOrder();
        Assert.Equal(3, wo.WorkflowVersion);
        Assert.Equal(["Design", "Mill", "Print", "Finish"], wo.Stages.Select(s => s.Name));
        Assert.All(wo.Stages, s => Assert.NotNull(s.StageDefinitionId));
    }

    [Fact]
    public void Route_without_stages_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => WorkflowEngine.CopyStages(new WorkOrder(), new WorkflowDefinition { Name = "Empty" }));
        Assert.Equal("Errors.Workflow.NoStages", ex.Message);
    }

    [Fact]
    public void Missing_data_keeps_the_order_incomplete_and_blocks_production()
    {
        var wo = NewOrder(complete: false);
        Assert.Equal(WorkOrderStatus.Incomplete, wo.Status);
        Assert.False(wo.DataComplete);
        Assert.Equal("Teeth", wo.MissingDataNotes);
        var ex = Assert.Throws<DomainException>(() => WorkflowEngine.Start(wo, Stage(wo, "Design"), User, Now));
        Assert.Equal("Errors.WorkOrder.DataIncomplete", ex.Message);

        var log = WorkflowEngine.ApplyDataCheck(wo, [], User, Now);
        Assert.Equal(TransitionAction.DataCompleted, log!.Action);
        Assert.Equal(WorkOrderStatus.Ready, wo.Status);
        Assert.Equal(Stage(wo, "Design").Id, wo.CurrentStageId);
    }

    [Fact]
    public void Ready_order_goes_back_to_incomplete_if_data_is_removed_before_production()
    {
        var wo = NewOrder();
        var log = WorkflowEngine.ApplyDataCheck(wo, [IntakeField.Shade], User, Now);
        Assert.Equal(WorkOrderStatus.Incomplete, wo.Status);
        Assert.Equal(WorkOrderStatus.Ready, log!.FromStatus);
    }

    [Fact]
    public void Steps_run_in_order_and_parallel_stages_must_all_finish()
    {
        var wo = NewOrder();
        var early = Assert.Throws<DomainException>(() => WorkflowEngine.Start(wo, Stage(wo, "Mill"), User, Now));
        Assert.Equal("Errors.WorkOrder.StageNotAvailable", early.Message);

        Finish(wo, "Design");
        Assert.Equal(WorkOrderStatus.InProduction, wo.Status);
        Assert.Equal(["Mill", "Print"], WorkflowEngine.AvailableStages(wo).Select(s => s.Name));

        Finish(wo, "Print");
        Assert.Equal(2, WorkflowEngine.CurrentStep(wo));
        Assert.Throws<DomainException>(() => WorkflowEngine.Start(wo, Stage(wo, "Finish"), User, Now));

        Finish(wo, "Mill");
        Finish(wo, "Finish");
        Assert.Equal(WorkOrderStatus.ReadyForDelivery, wo.Status);
        Assert.Equal(Now, wo.CompletedAtUtc);
        Assert.Null(wo.CurrentStageId);
    }

    [Fact]
    public void Start_assigns_the_stage_to_whoever_starts_it_and_pause_resume_are_logged()
    {
        var wo = NewOrder();
        var design = Stage(wo, "Design");
        var started = WorkflowEngine.Start(wo, design, User, Now);
        Assert.Equal(TransitionAction.Started, started.Action);
        Assert.Equal(User, design.AssignedUserId);

        WorkflowEngine.Pause(wo, design, User, Now, "lunch");
        Assert.Equal(StageStatus.Paused, design.Status);
        Assert.Equal(TransitionAction.Resumed, WorkflowEngine.Start(wo, design, User, Now).Action);
        Assert.Throws<DomainException>(() => WorkflowEngine.Start(wo, design, User, Now));
    }

    [Fact]
    public void Skip_needs_a_reason_and_opens_the_next_step()
    {
        var wo = NewOrder();
        var ex = Assert.Throws<DomainException>(() => WorkflowEngine.Skip(wo, Stage(wo, "Design"), User, Now, " "));
        Assert.Equal("Errors.ReasonRequired", ex.Message);

        WorkflowEngine.Skip(wo, Stage(wo, "Design"), User, Now, "Design supplied by clinic");
        Assert.Equal(StageStatus.Skipped, Stage(wo, "Design").Status);
        Assert.Equal(2, WorkflowEngine.CurrentStep(wo));
    }

    [Fact]
    public void Move_back_restarts_later_stages_and_only_goes_backwards()
    {
        var wo = NewOrder();
        Finish(wo, "Design");
        Finish(wo, "Mill");
        Finish(wo, "Print");

        Assert.Throws<DomainException>(() => WorkflowEngine.MoveBack(wo, Stage(wo, "Finish"), User, Now, "redo"));
        Assert.Throws<DomainException>(() => WorkflowEngine.MoveBack(wo, Stage(wo, "Design"), User, Now, null));

        var log = WorkflowEngine.MoveBack(wo, Stage(wo, "Design"), User, Now, "Margin open");
        Assert.Equal(TransitionAction.MovedBack, log.Action);
        Assert.All(wo.Stages, s => Assert.Equal(StageStatus.Pending, s.Status));
        Assert.Equal(1, WorkflowEngine.CurrentStep(wo));
        Assert.Equal(Stage(wo, "Design").Id, wo.CurrentStageId);
    }

    [Fact]
    public void Move_back_works_from_ready_for_delivery()
    {
        var wo = NewOrder();
        foreach (var name in new[] { "Design", "Mill", "Print", "Finish" })
        {
            Finish(wo, name);
        }

        WorkflowEngine.MoveBack(wo, Stage(wo, "Mill"), User, Now, "Shade mismatch");
        Assert.Equal(WorkOrderStatus.InProduction, wo.Status);
        Assert.Null(wo.CompletedAtUtc);
        Assert.Equal(StageStatus.Done, Stage(wo, "Design").Status);
        Assert.Equal(StageStatus.Pending, Stage(wo, "Print").Status);
    }

    [Fact]
    public void Hold_pauses_running_work_and_release_restores_the_previous_status()
    {
        var wo = NewOrder();
        WorkflowEngine.Start(wo, Stage(wo, "Design"), User, Now);
        var (hold, log) = WorkflowEngine.Hold(wo, Guid.NewGuid(), "Waiting for clinic", User, Now);
        Assert.Equal(WorkOrderStatus.OnHold, wo.Status);
        Assert.Equal(WorkOrderStatus.InProduction, hold.PreviousStatus);
        Assert.Equal(TransitionAction.Held, log.Action);
        Assert.Equal(StageStatus.Paused, Stage(wo, "Design").Status);
        Assert.Throws<DomainException>(() => WorkflowEngine.Start(wo, Stage(wo, "Design"), User, Now));
        Assert.Throws<DomainException>(() => WorkflowEngine.Hold(wo, Guid.NewGuid(), null, User, Now));

        WorkflowEngine.Release(wo, hold, User, Now);
        Assert.Equal(WorkOrderStatus.InProduction, wo.Status);
        Assert.Equal(Now, hold.ResumedAtUtc);
        var again = Assert.Throws<DomainException>(() => WorkflowEngine.Release(wo, hold, User, Now));
        Assert.Equal("Errors.WorkOrder.NotOnHold", again.Message);
    }

    [Fact]
    public void Cancel_needs_a_reason_and_closes_the_order()
    {
        var wo = NewOrder();
        Assert.Throws<DomainException>(() => WorkflowEngine.Cancel(wo, User, Now, ""));
        WorkflowEngine.Cancel(wo, User, Now, "Clinic cancelled");
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
        Assert.False(wo.IsOpen);
        Assert.Throws<DomainException>(() => WorkflowEngine.Cancel(wo, User, Now, "again"));
        Assert.Throws<DomainException>(() => WorkflowEngine.Start(wo, Stage(wo, "Design"), User, Now));
    }
}

public sealed class FdiTests
{
    [Theory]
    [InlineData("11", "11")]
    [InlineData(" 48 ", "48")]
    [InlineData("85", "85")]
    [InlineData("upper", "UPPER")]
    [InlineData("LOWER", "LOWER")]
    [InlineData("19", null)]
    [InlineData("49", null)]
    [InlineData("56", null)]
    [InlineData("91", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_accepts_only_valid_fdi_codes(string? input, string? expected) => Assert.Equal(expected, Fdi.Normalize(input));

    [Fact]
    public void Chart_has_32_permanent_and_20_primary_teeth()
    {
        Assert.Equal(32, Fdi.Permanent.Count);
        Assert.Equal(20, Fdi.Primary.Count);
        Assert.Equal(["UPPER", "11", "21", "48"], new[] { "48", "21", "UPPER", "11" }.OrderBy(Fdi.Order));
    }
}

public sealed class FileInspectorTests
{
    [Fact]
    public void Accepts_files_whose_content_matches_the_extension()
    {
        Assert.Equal("image/jpeg", FileInspector.Inspect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1 }, "photo.JPG")!.ContentType);
        Assert.Equal("application/pdf", FileInspector.Inspect("%PDF-1.7\n"u8, "rx.pdf")!.ContentType);
        Assert.Equal("stl", FileInspector.Inspect("solid crown\n facet normal 0 0 1\nendsolid"u8, "crown.stl")!.Kind);

        var binaryStl = new byte[84 + 50];
        BitConverter.GetBytes(1u).CopyTo(binaryStl, 80);
        Assert.Equal("stl", FileInspector.Inspect(binaryStl, "scan.stl")!.Kind);
    }

    [Theory]
    [InlineData("crown.stl", "MZ this is an exe")]
    [InlineData("photo.jpg", "%PDF-1.4")]
    [InlineData("tool.exe", "MZ")]
    [InlineData("notes.txt", "hello")]
    [InlineData("noext", "solid facet")]
    public void Rejects_disguised_or_unsupported_files(string name, string content) =>
        Assert.Null(FileInspector.Inspect(Encoding.ASCII.GetBytes(content), name));

    [Fact]
    public void Rejects_empty_files() => Assert.Null(FileInspector.Inspect([], "a.pdf"));

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("/var/data/scan.stl", "scan.stl")]
    [InlineData("bad\"name\r\n.pdf", "badname.pdf")]
    [InlineData("   ", "file")]
    public void Safe_name_drops_paths_and_control_characters(string input, string expected) => Assert.Equal(expected, FileInspector.SafeName(input));
}

public sealed class LabCalendarTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(24, 3)]
    public void Due_date_suggestion_uses_eight_hour_days(decimal hours, int days) =>
        Assert.Equal(new DateOnly(2026, 10, 26).AddDays(days), LabCalendar.SuggestDueDate(new DateOnly(2026, 10, 26), hours));
}

public sealed class QualityEngineTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Build → QC (quality pass) → Pack.</summary>
    private static WorkOrder NewOrder()
    {
        var wf = new WorkflowDefinition { Id = Guid.NewGuid(), Name = "QC route", RequireTeeth = false, RequireDentist = false };
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 1, Name = "Build" });
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 2, Name = "QC", RequiresQualityPass = true });
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 3, Name = "Pack" });
        var wo = new WorkOrder { Id = Guid.NewGuid(), Number = "WO-26-000009" };
        WorkflowEngine.CopyStages(wo, wf);
        WorkflowEngine.ApplyDataCheck(wo, [], User, Now);
        return wo;
    }

    private static WorkOrderStage Stage(WorkOrder wo, string name) => wo.Stages.Single(s => s.Name == name);

    private static void Work(WorkOrder wo, string name)
    {
        WorkflowEngine.Start(wo, Stage(wo, name), User, Now);
        WorkflowEngine.Complete(wo, Stage(wo, name), User, Now);
    }

    private static WorkOrder AtQuality()
    {
        var wo = NewOrder();
        Work(wo, "Build");
        Work(wo, "QC");
        return wo;
    }

    [Fact]
    public void Completing_a_quality_stage_sends_it_to_inspection_instead_of_finishing_it()
    {
        var wo = AtQuality();
        Assert.Equal(StageStatus.AwaitingQuality, Stage(wo, "QC").Status);
        Assert.Equal(WorkOrderStatus.InQuality, wo.Status);
        Assert.Null(Stage(wo, "QC").CompletedAtUtc);

        // The next step stays closed until quality decides.
        Assert.Equal([Stage(wo, "QC")], WorkflowEngine.AvailableStages(wo));
        Assert.Throws<DomainException>(() => WorkflowEngine.Start(wo, Stage(wo, "Pack"), User, Now));
    }

    [Fact]
    public void Acceptance_finishes_the_stage_and_opens_the_next_step()
    {
        var wo = AtQuality();
        var log = WorkflowEngine.Inspect(wo, Stage(wo, "QC"), QualityResult.Accepted, false, null, User, Now, null);
        Assert.Equal(TransitionAction.QualityAccepted, log.Action);
        Assert.Equal(StageStatus.Done, Stage(wo, "QC").Status);
        Assert.Equal(WorkOrderStatus.InProduction, wo.Status);
        Assert.Equal(Stage(wo, "Pack").Id, wo.CurrentStageId);
        Work(wo, "Pack");
        Assert.Equal(WorkOrderStatus.ReadyForDelivery, wo.Status);
    }

    [Fact]
    public void A_failed_critical_item_blocks_acceptance()
    {
        var wo = AtQuality();
        var ex = Assert.Throws<DomainException>(() => WorkflowEngine.Inspect(wo, Stage(wo, "QC"), QualityResult.Accepted, true, null, User, Now, null));
        Assert.Equal("Errors.Quality.CriticalFailed", ex.Message);
        Assert.Equal(StageStatus.AwaitingQuality, Stage(wo, "QC").Status);
    }

    [Fact]
    public void Rejection_needs_a_reason_and_returns_the_work_to_an_earlier_stage_keeping_history()
    {
        var wo = AtQuality();
        Assert.Throws<DomainException>(() => WorkflowEngine.Inspect(wo, Stage(wo, "QC"), QualityResult.Rejected, true, Stage(wo, "Build"), User, Now, " "));
        Assert.Throws<DomainException>(() => WorkflowEngine.Inspect(wo, Stage(wo, "QC"), QualityResult.Rejected, true, Stage(wo, "Pack"), User, Now, "Crack"));

        var log = WorkflowEngine.Inspect(wo, Stage(wo, "QC"), QualityResult.Rejected, true, Stage(wo, "Build"), User, Now, "Crack");
        Assert.Equal(TransitionAction.QualityRejected, log.Action);
        Assert.Equal(WorkOrderStatus.InQuality, log.FromStatus);
        Assert.Equal(WorkOrderStatus.InProduction, wo.Status);
        Assert.Equal("Crack", log.Reason);
        Assert.All(wo.Stages, s => Assert.Equal(StageStatus.Pending, s.Status));
        Assert.Equal(Stage(wo, "Build").Id, wo.CurrentStageId);
    }

    [Fact]
    public void An_adjustment_can_return_to_the_inspected_stage_itself()
    {
        var wo = AtQuality();
        var log = WorkflowEngine.Inspect(wo, Stage(wo, "QC"), QualityResult.AdjustmentRequested, false, Stage(wo, "QC"), User, Now, "Contacts");
        Assert.Equal(TransitionAction.AdjustmentRequested, log.Action);
        Assert.Equal(StageStatus.Done, Stage(wo, "Build").Status);
        Assert.Equal(StageStatus.Pending, Stage(wo, "QC").Status);
        Assert.Equal([Stage(wo, "QC")], WorkflowEngine.AvailableStages(wo));
    }

    [Fact]
    public void Only_a_stage_awaiting_quality_can_be_inspected()
    {
        var wo = NewOrder();
        var ex = Assert.Throws<DomainException>(() => WorkflowEngine.Inspect(wo, Stage(wo, "Build"), QualityResult.Accepted, false, null, User, Now, null));
        Assert.Equal("Errors.Quality.NotAwaiting", ex.Message);
    }

    [Fact]
    public void A_remake_scraps_work_still_in_the_lab_but_keeps_a_delivered_order_delivered()
    {
        var inLab = AtQuality();
        var log = WorkflowEngine.ReplaceByRemake(inLab, User, Now, "WO-26-000010 — broken");
        Assert.Equal(WorkOrderStatus.Rejected, inLab.Status);
        Assert.False(inLab.IsOpen);
        Assert.Equal(TransitionAction.ReplacedByRemake, log.Action);
        Assert.Throws<DomainException>(() => WorkflowEngine.ReplaceByRemake(inLab, User, Now, "again"));

        var delivered = AtQuality();
        WorkflowEngine.Inspect(delivered, Stage(delivered, "QC"), QualityResult.Accepted, false, null, User, Now, null);
        Work(delivered, "Pack");
        WorkflowEngine.MarkDelivered(delivered, User, Now, "Reception");
        WorkflowEngine.ReplaceByRemake(delivered, User, Now, "remake");
        Assert.Equal(WorkOrderStatus.Delivered, delivered.Status);
    }

    [Fact]
    public void Orders_on_a_delivery_note_cannot_be_moved_held_or_cancelled_and_only_ready_orders_are_delivered()
    {
        var wo = AtQuality();
        Assert.Throws<DomainException>(() => WorkflowEngine.MarkDelivered(wo, User, Now, null));
        WorkflowEngine.Inspect(wo, Stage(wo, "QC"), QualityResult.Accepted, false, null, User, Now, null);
        Work(wo, "Pack");
        wo.DeliveryNoteId = Guid.NewGuid();
        Assert.Equal("Errors.WorkOrder.InDelivery",
            Assert.Throws<DomainException>(() => WorkflowEngine.MoveBack(wo, Stage(wo, "Build"), User, Now, "why")).Message);
        Assert.Throws<DomainException>(() => WorkflowEngine.Hold(wo, Guid.NewGuid(), null, User, Now));
        Assert.Throws<DomainException>(() => WorkflowEngine.Cancel(wo, User, Now, "why"));

        var log = WorkflowEngine.MarkDelivered(wo, User, Now, "Dr. Sara");
        Assert.Equal(WorkOrderStatus.Delivered, wo.Status);
        Assert.Null(wo.DeliveryNoteId);
        Assert.Equal(Now, wo.DeliveredAtUtc);
        Assert.Equal("Dr. Sara", log.Reason);
    }

    [Fact]
    public void A_parallel_stage_can_still_be_worked_while_its_sibling_waits_for_inspection()
    {
        var wf = new WorkflowDefinition { Id = Guid.NewGuid(), Name = "P", RequireTeeth = false, RequireDentist = false };
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 1, Name = "A", RequiresQualityPass = true });
        wf.Stages.Add(new WorkflowStageDefinition { Id = Guid.NewGuid(), Step = 1, Name = "B" });
        var wo = new WorkOrder { Id = Guid.NewGuid(), Number = "WO-26-000011" };
        WorkflowEngine.CopyStages(wo, wf);
        WorkflowEngine.ApplyDataCheck(wo, [], User, Now);
        Work(wo, "A");
        Assert.Equal(WorkOrderStatus.InQuality, wo.Status);
        WorkflowEngine.Start(wo, Stage(wo, "B"), User, Now);
        Assert.Equal(WorkOrderStatus.InQuality, wo.Status);
        WorkflowEngine.Complete(wo, Stage(wo, "B"), User, Now);
        WorkflowEngine.Inspect(wo, Stage(wo, "A"), QualityResult.Accepted, false, null, User, Now, null);
        Assert.Equal(WorkOrderStatus.ReadyForDelivery, wo.Status);
    }
}
