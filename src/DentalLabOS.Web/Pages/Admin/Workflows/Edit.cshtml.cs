using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Workflows;

/// <summary>ADM-16 route designer. Stages sharing a step run in parallel; saving bumps the version and only new work orders use it.</summary>
[Screen("ADM-16")]
[HasPermission(Permissions.Admin.WorkflowsManage)]
public sealed class EditModel(WorkflowService workflows, ProductionService production, QualityService quality) : AppPageModel
{
    private const int BlankRows = 2;

    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";
    [BindProperty] public InputModel Input { get; set; } = new();

    public bool IsNew => !Guid.TryParse(Id, out _);
    public WorkflowDto? Current { get; private set; }
    public IReadOnlyList<OptionDto> Departments { get; private set; } = [];
    public IReadOnlyList<OptionDto> Templates { get; private set; } = [];

    public sealed class StageInput
    {
        public Guid? Id { get; set; }
        public int Step { get; set; }
        public string? Name { get; set; }
        public Guid? DepartmentId { get; set; }
        public decimal TargetHours { get; set; }
        public bool RequiresQualityPass { get; set; }
        public Guid? QualityTemplateId { get; set; }
    }

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; }
        public bool RequireDentist { get; set; } = true;
        public bool RequireTeeth { get; set; } = true;
        public bool RequireShade { get; set; }
        public bool RequireMaterial { get; set; }
        public bool RequirePatientReference { get; set; }
        public bool RequireAttachment { get; set; }
        public List<StageInput> Stages { get; set; } = [];
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Current is { } w)
        {
            Input = new InputModel
            {
                Name = w.Name, Description = w.Description, IsActive = w.IsActive, IsDefault = w.IsDefault, RequireDentist = w.RequireDentist,
                RequireTeeth = w.RequireTeeth, RequireShade = w.RequireShade, RequireMaterial = w.RequireMaterial,
                RequirePatientReference = w.RequirePatientReference, RequireAttachment = w.RequireAttachment, RowVersion = w.RowVersion,
                Stages = w.Stages.Select(s => new StageInput
                {
                    Id = s.Id, Step = s.Step, Name = s.Name, DepartmentId = s.DepartmentId, TargetHours = s.TargetHours, RequiresQualityPass = s.RequiresQualityPass,
                    QualityTemplateId = s.QualityTemplateId,
                }).ToList(),
            };
        }

        PadStages();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        var stages = i.Stages.Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => new WorkflowStageRequest(s.Step <= 0 ? 1 : s.Step, s.Name!, s.DepartmentId, s.TargetHours, s.RequiresQualityPass, s.Id, s.QualityTemplateId)).ToList();
        var request = new WorkflowUpsertRequest(i.Name, stages, i.Description, i.IsActive, i.IsDefault, i.RequireDentist, i.RequireTeeth, i.RequireShade,
            i.RequireMaterial, i.RequirePatientReference, i.RequireAttachment, i.RowVersion);
        WorkflowDto? saved = null;
        if (!await TryExecuteAsync(async () => saved = await workflows.SaveAsync(IsNew ? null : Guid.Parse(Id), request)))
        {
            await LoadAsync();
            PadStages();
            return Page();
        }

        Flash(IsNew ? "Workflows.Created" : "Workflows.Saved", saved!.Version);
        return Redirect($"/admin/workflows/{saved.Id}");
    }

    private void PadStages()
    {
        var next = Input.Stages.Count == 0 ? 1 : Input.Stages.Max(s => s.Step) + 1;
        for (var k = 0; k < BlankRows; k++)
        {
            Input.Stages.Add(new StageInput { Step = next + k });
        }
    }

    private async Task LoadAsync()
    {
        Departments = await production.DepartmentOptionsAsync();
        Templates = await quality.TemplateOptionsAsync();
        if (!IsNew)
        {
            Current = await workflows.GetAsync(Guid.Parse(Id));
        }
    }
}
