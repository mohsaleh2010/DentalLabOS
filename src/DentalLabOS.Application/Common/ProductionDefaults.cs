using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Common;

/// <summary>
/// Starter production setup for a lab: departments, the three example routes of plan section 4 and common reason codes.
/// The routes are examples to be reviewed by the lab's technicians; everything is editable. Runs inside the tenant's scope.
/// </summary>
public static class ProductionDefaults
{
    private static readonly (string Code, string Ar, string En)[] Departments =
    [
        ("RCV", "الاستقبال", "Reception"), ("CAD", "التصميم الرقمي CAD/CAM", "CAD/CAM design"), ("CER", "السيراميك", "Ceramics"),
        ("REM", "التركيبات المتحركة", "Removables"), ("MTL", "المعادن", "Metal"), ("QC", "الجودة", "Quality control"),
    ];

    private static readonly (string Ar, string En, bool Default, bool Shade, (string Ar, string En, string Dept, decimal Hours, bool Qc)[] Stages)[] Workflows =
    [
        ("تركيبات ثابتة: زيركون وسيراميك", "Fixed: zirconia & ceramic", true, true,
        [
            ("استلام وفحص البيانات", "Intake & data check", "RCV", 1, false), ("التصميم CAD", "CAD design", "CAD", 4, false),
            ("التصنيع CAM / تجهيز الهيكل", "CAM milling / framework", "CAD", 6, false), ("التلبيد والمعالجة", "Sintering", "CER", 8, false),
            ("التلوين والتشطيب", "Staining & finishing", "CER", 4, false), ("فحص الجودة", "Quality check", "QC", 1, true),
            ("التعبئة والتجهيز للتسليم", "Packing", "RCV", 0.5m, false),
        ]),
        ("تركيبات متحركة", "Removable prosthetics", false, false,
        [
            ("استلام الطبعة أو الملف", "Impression intake", "RCV", 1, false), ("إعداد النموذج", "Model preparation", "REM", 4, false),
            ("التصميم والتجربة", "Design & try-in", "REM", 8, false), ("تجهيز الهيكل أو القاعدة", "Base / framework", "REM", 8, false),
            ("ترتيب الأسنان", "Teeth setup", "REM", 6, false), ("المعالجة", "Processing", "REM", 6, false),
            ("التشطيب", "Finishing", "REM", 3, false), ("الفحص والتسليم", "Quality check & packing", "QC", 1, true),
        ]),
        ("تركيبات معدنية وجسور", "Metal & bridges", false, true,
        [
            ("الاستلام", "Intake", "RCV", 1, false), ("التصميم", "Design", "CAD", 4, false),
            ("تصنيع الهيكل", "Framework", "MTL", 8, false), ("الفحص / التجربة", "Fit check / try-in", "MTL", 2, false),
            ("التغطية أو الإنهاء", "Veneering / finishing", "CER", 6, false), ("فحص الجودة", "Quality check", "QC", 1, true),
            ("التسليم", "Packing", "RCV", 0.5m, false),
        ]),
    ];

    private static readonly (ReasonScope Scope, ReasonCategory Category, string Ar, string En)[] Reasons =
    [
        (ReasonScope.Hold, ReasonCategory.DentistData, "انتظار رد الطبيب", "Waiting for the dentist"),
        (ReasonScope.Hold, ReasonCategory.DentistData, "بيانات أو طبعة ناقصة", "Missing data or impression"),
        (ReasonScope.Hold, ReasonCategory.Material, "انتظار خامة", "Waiting for material"),
        (ReasonScope.Hold, ReasonCategory.Manufacturing, "عطل جهاز", "Equipment breakdown"),
        (ReasonScope.Hold, ReasonCategory.Unspecified, "طلب العيادة التأجيل", "Clinic asked to postpone"),
        (ReasonScope.Cancellation, ReasonCategory.DentistData, "ألغت العيادة الحالة", "Cancelled by the clinic"),
        (ReasonScope.Cancellation, ReasonCategory.Unspecified, "إدخال مكرر أو خاطئ", "Duplicate or wrong entry"),
        (ReasonScope.Rejection, ReasonCategory.Manufacturing, "عيب تصنيع", "Manufacturing defect"),
        (ReasonScope.Rejection, ReasonCategory.Design, "خطأ تصميم", "Design error"),
        (ReasonScope.Remake, ReasonCategory.DentistData, "طبعة أو قياس غير دقيق", "Inaccurate impression or scan"),
        (ReasonScope.Remake, ReasonCategory.Manufacturing, "كسر أثناء التصنيع", "Broken during production"),
        (ReasonScope.Adjustment, ReasonCategory.Design, "تعديل إطباق أو نقاط تماس", "Occlusion / contact adjustment"),
        (ReasonScope.Waste, ReasonCategory.Material, "تلف خامة", "Material damaged"),
        (ReasonScope.Rejection, ReasonCategory.Material, "عيب في الخامة أو اللون", "Material or shade defect"),
        (ReasonScope.Adjustment, ReasonCategory.Manufacturing, "تعديل تشطيب أو لون", "Finish or shade touch-up"),
        (ReasonScope.Adjustment, ReasonCategory.DentistData, "طلب الطبيب تعديلًا", "Dentist asked for a change"),
        (ReasonScope.DeliveryFailure, ReasonCategory.Transport, "العيادة مغلقة", "Clinic closed"),
        (ReasonScope.DeliveryFailure, ReasonCategory.Transport, "لا يوجد مستلم", "No one to receive it"),
        (ReasonScope.DeliveryFailure, ReasonCategory.Transport, "عنوان غير صحيح", "Wrong address"),
    ];

    /// <summary>A starter final-inspection checklist (plan 5.6); critical items block acceptance.</summary>
    private static readonly (string Ar, string En, bool Critical)[] Checklist =
    [
        ("مطابقة الحالة للوصفة والأسنان المطلوبة", "Matches the prescription and teeth", true),
        ("اللون مطابق للمطلوب", "Shade matches", true),
        ("الانطباق والحواف سليمة على النموذج", "Fit and margins on the model", true),
        ("نقاط التماس والإطباق", "Contacts and occlusion", false),
        ("الشكل والتشطيب واللمعان", "Anatomy, finish and glaze", false),
        ("لا كسور أو فقاعات أو تشققات", "No cracks, bubbles or chips", true),
        ("التنظيف والتعقيم قبل التغليف", "Cleaned and disinfected before packing", false),
    ];

    public static void Add(IApplicationDbContext db, string culture)
    {
        AddMissing(db, culture, [], false, false);
        AddChecklist(db, culture);
    }

    /// <summary>Adds what an existing lab is missing (idempotent; call inside the tenant's scope).</summary>
    public static async Task<bool> BackfillAsync(IApplicationDbContext db, string culture, CancellationToken ct)
    {
        var hasWorkflows = await db.Workflows.AnyAsync(ct);
        var hasReasons = await db.ReasonCodes.AnyAsync(ct);
        var changed = false;

        // Phase 4 additions for labs created earlier: the starter checklist and the delivery-failure reasons.
        if (!await db.QualityTemplates.AnyAsync(ct))
        {
            AddChecklist(db, culture);
            changed = true;
        }

        if (hasReasons && !await db.ReasonCodes.AnyAsync(r => r.Scope == ReasonScope.DeliveryFailure, ct))
        {
            var ar = culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            foreach (var r in Reasons.Where(r => r.Scope == ReasonScope.DeliveryFailure))
            {
                db.ReasonCodes.Add(new ReasonCode { Scope = r.Scope, Category = r.Category, Name = ar ? r.Ar : r.En });
            }

            changed = true;
        }

        if (hasWorkflows && hasReasons)
        {
            return changed;
        }

        var departments = (await db.Departments.Select(d => new { d.Code, d.Id, d.BranchId }).ToListAsync(ct))
            .OrderBy(d => d.BranchId is not null).GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        AddMissing(db, culture, departments, hasWorkflows, hasReasons);
        return true;
    }

    private static void AddChecklist(IApplicationDbContext db, string culture)
    {
        var ar = culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var template = new QualityTemplate
        {
            Name = ar ? "الفحص النهائي القياسي" : "Standard final inspection", IsDefault = true,
            Description = ar ? "قائمة بداية؛ راجعها مع مسؤول الجودة." : "A starting list; review it with your quality lead.",
        };
        var order = 1;
        foreach (var (a, e, critical) in Checklist)
        {
            template.Items.Add(new QualityTemplateItem { QualityTemplateId = template.Id, Order = order++, Text = ar ? a : e, IsCritical = critical });
        }

        db.QualityTemplates.Add(template);
    }

    private static void AddMissing(IApplicationDbContext db, string culture, Dictionary<string, Guid> departments, bool skipWorkflows, bool skipReasons)
    {
        var ar = culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        string Name(string a, string e) => ar ? a : e;

        if (!skipWorkflows)
        {
            foreach (var d in Departments.Where(d => !departments.ContainsKey(d.Code)))
            {
                var dept = new Department { Code = d.Code, Name = Name(d.Ar, d.En), IsProduction = d.Code != "RCV" };
                db.Departments.Add(dept);
                departments[d.Code] = dept.Id;
            }

            foreach (var w in Workflows)
            {
                var wf = new WorkflowDefinition
                {
                    Name = Name(w.Ar, w.En), IsDefault = w.Default, RequireDentist = true, RequireTeeth = true, RequireShade = w.Shade,
                };
                var step = 1;
                foreach (var s in w.Stages)
                {
                    wf.Stages.Add(new WorkflowStageDefinition
                    {
                        WorkflowDefinitionId = wf.Id, Step = step++, Name = Name(s.Ar, s.En), DepartmentId = departments[s.Dept],
                        TargetHours = s.Hours, RequiresQualityPass = s.Qc,
                    });
                }

                db.Workflows.Add(wf);
            }
        }

        if (!skipReasons)
        {
            foreach (var r in Reasons)
            {
                db.ReasonCodes.Add(new ReasonCode { Scope = r.Scope, Category = r.Category, Name = Name(r.Ar, r.En) });
            }
        }
    }
}
