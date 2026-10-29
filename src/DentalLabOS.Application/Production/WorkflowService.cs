using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Production;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Production;

/// <summary>
/// Production route templates (ADM-15/16). Saving bumps the version; work orders keep the copy of the stages they
/// started with, so a change only affects new work orders. One active default route per lab.
/// </summary>
public sealed class WorkflowService(IApplicationDbContext db, IPermissionService permissions)
{
    public async Task<IReadOnlyList<WorkflowListItemDto>> ListAsync(CancellationToken ct = default)
    {
        await DemandReadAsync(ct);
        return await db.Workflows.AsNoTracking().OrderByDescending(w => w.IsDefault).ThenByDescending(w => w.IsActive).ThenBy(w => w.Name)
            .Select(w => new WorkflowListItemDto(w.Id, w.Name, w.Description, w.Version, w.IsActive, w.IsDefault, w.Stages.Count,
                db.Items.Count(i => i.DefaultWorkflowId == w.Id),
                db.WorkOrders.Count(o => o.WorkflowDefinitionId == w.Id && o.Status != WorkOrderStatus.Delivered && o.Status != WorkOrderStatus.Cancelled && o.Status != WorkOrderStatus.Rejected)))
            .ToListAsync(ct);
    }

    /// <summary>Active routes for pickers (item form, intake).</summary>
    public async Task<IReadOnlyList<OptionDto>> OptionsAsync(CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(Permissions.Items.Manage, ct))
        {
            await DemandReadAsync(ct);
        }

        return await db.Workflows.AsNoTracking().Where(w => w.IsActive).OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name)
            .Select(w => new OptionDto(w.Id, w.IsDefault ? "*" : string.Empty, w.Name)).ToListAsync(ct);
    }

    public async Task<WorkflowDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await DemandReadAsync(ct);
        var w = await db.Workflows.AsNoTracking().Include(x => x.Stages).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException(nameof(WorkflowDefinition));
        var deptIds = w.Stages.Where(s => s.DepartmentId != null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var depts = await db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        return Map(w, depts, await TemplateNamesAsync(ct));
    }

    private Task<Dictionary<Guid, string>> TemplateNamesAsync(CancellationToken ct) =>
        db.QualityTemplates.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);

    public async Task<WorkflowDto> SaveAsync(Guid? id, WorkflowUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.WorkflowsManage, ct);
        var name = request.Name?.Trim();
        var stages = request.Stages ?? [];
        var v = new Validator().Required(nameof(request.Name), name).MaxLength(nameof(request.Name), name, 150)
            .MaxLength(nameof(request.Description), request.Description, 500)
            .When(stages.Count == 0, nameof(request.Stages), "Validation.WorkflowStages")
            .When(stages.Count > 40, nameof(request.Stages), "Validation.TooMany")
            .When(request.IsDefault && !request.IsActive, nameof(request.IsDefault), "Validation.DefaultInactive");
        for (var i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            v.Required($"Stages[{i}].Name", s.Name).MaxLength($"Stages[{i}].Name", s.Name, 100)
                .Range($"Stages[{i}].Step", s.Step, 1, 100).Range($"Stages[{i}].TargetHours", s.TargetHours, 0, 1000);
        }

        var deptIds = stages.Where(s => s.DepartmentId != null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var knownDepts = await db.Departments.Where(d => deptIds.Contains(d.Id)).CountAsync(ct);
        v.When(knownDepts != deptIds.Count, nameof(request.Stages), "Validation.Required");
        var templateIds = stages.Where(s => s.RequiresQualityPass && s.QualityTemplateId != null).Select(s => s.QualityTemplateId!.Value).Distinct().ToList();
        var knownTemplates = await db.QualityTemplates.Where(t => templateIds.Contains(t.Id) && t.IsActive).CountAsync(ct);
        v.When(knownTemplates != templateIds.Count, nameof(request.Stages), "Validation.QualityTemplate");
        v.When(name is not null && await db.Workflows.AnyAsync(w => w.Name == name && w.Id != id, ct), nameof(request.Name), "Validation.NameTaken");

        WorkflowDefinition? wf = null;
        if (id is { } existing)
        {
            wf = await db.Workflows.Include(w => w.Stages).FirstOrDefaultAsync(w => w.Id == existing, ct) ?? throw new NotFoundException(nameof(WorkflowDefinition));
            v.When(wf.IsDefault && !request.IsDefault, nameof(request.IsDefault), "Validation.DefaultRequired");
        }

        v.ThrowIfInvalid();

        return await db.InTransactionAsync(async innerCt =>
        {
            if (request.IsDefault && wf?.IsDefault != true)
            {
                // Clear the old default first: a filtered unique index allows only one.
                var old = await db.Workflows.Where(w => w.IsDefault).ToListAsync(innerCt);
                old.ForEach(w => w.IsDefault = false);
                await db.SaveChangesAsync(innerCt);
            }

            if (wf is null)
            {
                wf = new WorkflowDefinition();
                db.Workflows.Add(wf);
            }
            else
            {
                db.SetOriginalRowVersion(wf, request.RowVersion);
                wf.Version++;
            }

            wf.Name = name!;
            wf.Description = MasterData.Clean(request.Description);
            wf.IsActive = request.IsActive;
            wf.IsDefault = request.IsDefault;
            wf.RequireDentist = request.RequireDentist;
            wf.RequireTeeth = request.RequireTeeth;
            wf.RequireShade = request.RequireShade;
            wf.RequireMaterial = request.RequireMaterial;
            wf.RequirePatientReference = request.RequirePatientReference;
            wf.RequireAttachment = request.RequireAttachment;

            // Renumber steps 1..n keeping parallel stages together.
            var stepMap = stages.Select(s => s.Step).Distinct().Order().Select((step, i) => (step, i + 1)).ToDictionary(x => x.step, x => x.Item2);
            var keep = new HashSet<Guid>();
            foreach (var s in stages)
            {
                var row = s.Id is { } sid ? wf.Stages.FirstOrDefault(x => x.Id == sid) : null;
                if (row is null)
                {
                    row = new WorkflowStageDefinition { WorkflowDefinitionId = wf.Id };
                    wf.Stages.Add(row);
                }

                keep.Add(row.Id);
                row.Step = stepMap[s.Step];
                row.Name = s.Name.Trim();
                row.DepartmentId = s.DepartmentId;
                row.TargetHours = s.TargetHours;
                row.RequiresQualityPass = s.RequiresQualityPass;
                row.QualityTemplateId = s.RequiresQualityPass ? s.QualityTemplateId : null;
            }

            var removed = wf.Stages.Where(x => !keep.Contains(x.Id)).ToList();
            if (removed.Count > 0)
            {
                var removedIds = removed.Select(r => r.Id).ToList();

                // Running work orders keep their copied stage; only the link to the template goes.
                await db.WorkOrderStages.Where(s => s.StageDefinitionId != null && removedIds.Contains(s.StageDefinitionId.Value))
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.StageDefinitionId, (Guid?)null), innerCt);
                foreach (var r in removed)
                {
                    wf.Stages.Remove(r);
                    db.WorkflowStages.Remove(r);
                }
            }

            await db.SaveChangesAsync(innerCt);
            var names = await db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, innerCt);
            return Map(wf, names, await TemplateNamesAsync(innerCt));
        }, ct);
    }

    private async Task DemandReadAsync(CancellationToken ct)
    {
        if (!await permissions.HasAsync(Permissions.Admin.WorkflowsManage, ct))
        {
            await permissions.DemandAsync(Permissions.Production.Read, ct);
        }
    }

    private static WorkflowDto Map(WorkflowDefinition w, IReadOnlyDictionary<Guid, string> depts, IReadOnlyDictionary<Guid, string> templates) =>
        new(w.Id, w.Name, w.Description, w.Version, w.IsActive, w.IsDefault, w.RequireDentist, w.RequireTeeth, w.RequireShade, w.RequireMaterial,
            w.RequirePatientReference, w.RequireAttachment,
            w.Stages.OrderBy(s => s.Step).ThenBy(s => s.Name, StringComparer.Ordinal)
                .Select(s => new WorkflowStageDto(s.Id, s.Step, s.Name, s.DepartmentId, s.DepartmentId is { } d && depts.TryGetValue(d, out var n) ? n : null,
                    s.TargetHours, s.RequiresQualityPass, s.QualityTemplateId,
                    s.QualityTemplateId is { } t && templates.TryGetValue(t, out var tn) ? tn : null))
                .ToList(),
            RowVersions.Encode(w.RowVersion));
}
