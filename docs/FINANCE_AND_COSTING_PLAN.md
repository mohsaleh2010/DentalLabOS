# خطة الحسابات والتكاليف — مراجعة التغطية والإضافات

> آخر تحديث: 2026-10-29 · وثيقة تخطيط فقط، لا كود. تكمّل [EXECUTION_PLAN.md](EXECUTION_PLAN.md) ولا تغيّر ترتيب المراحل.
> المرجع: `DentalLabOS_SaaS_Implementation_Plan_v3.md` (الأقسام A2.5–A2.10، A4، B8–B12).

## 1. الخلاصة

الأربعة المطلوبة **موجودة في الخطة** بشاشاتها وكياناتها، لكن لم يُبنَ منها بعد إلا البيانات الأساسية. عند المراجعة ظهرت **ثماني فجوات** ستسبب مشاكل لو بدأنا البناء كما هو مكتوب، أهمها أن كشوف حساب العملاء والموردين مخططة قبل المحاسبة (المراحل 6–8) دون مصدر موحد للحركات، وأن التكاليف غير المباشرة لها قاعدة توزيع دون أي ربط بالمصروفات الفعلية.

| البند | في الخطة | المرحلة | المبني الآن |
|---|---|---|---|
| حساب العملاء (ذمم، كشف حساب، أعمار، ائتمان) | CUS-06/07، SAL-10، PAY-01/02/05/08، ACC-16 | 7 و8 و11 | ملف العميل، حد الائتمان (بصلاحية مستقلة)، شروط السداد، حقل رصيد افتتاحي، `ReceivableAccountId` |
| حساب الموردين (ذمم، كشف حساب، أعمار) | SUP-04، PUR-07/08/09/10، PAY-03/04 | 6 و8 و11 | ملف المورد، أصناف المورد وأسعاره، حقل رصيد افتتاحي، `PayableAccountId` |
| المصروفات | EXP-01..06، ADM-20، كيانات B11 | 9 | مفاتيح الصلاحيات فقط، ومنطق `ApprovalPolicy` في المجال |
| تكلفة الإنتاج المباشرة | A2.10: مواد + عمالة + خدمات خارجية | 5 و6 و10 | `TechnicianProfile.LaborRatePerHour`، `TaskTimeEntry` مع `LaborRateSnapshot` (المرحلة 3)، BOM بتكلفة معيارية |
| تكلفة الإنتاج غير المباشرة | ADM-19، `OverheadRuleDefinition` | 10 | مفتاح `admin.costing.manage` فقط |

## 2. الفجوات

| # | الفجوة | الأثر لو تُركت |
|---|---|---|
| F1 | لا يوجد دفتر مساعد للعملاء والموردين. الحركات التفصيلية بالطرف موجودة فقط في `JournalLine` (المرحلة 11)، بينما كشوف الحساب والأعمار مطلوبة من المرحلة 6. | كل شاشة كشف حساب ستبني استعلامًا خاصًا يجمع الفواتير والسندات والمرتجعات، وتختلف الأرصدة بين الشاشات، ثم يُعاد بناؤها كلها في المرحلة 11. |
| F2 | الرصيد الافتتاحي للعميل والمورد حقل نصي قابل للتعديل في النموذج، ولا يتحول لحركة قبل المحاسبة. ولا تفصيل له بالفواتير المفتوحة. | الرصيد يتغير بلا أثر في التدقيق المالي، وأعمار الديون تبدأ خاطئة لأن الرصيد كله بتاريخ واحد. |
| F3 | المصروف الآجل ينشئ ذمة على مورد، لكن `PaymentAllocation` لا يقبل إلا فاتورة مبيعات أو فاتورة مورد. | لا يمكن سداد مصروف آجل وتخصيص الدفعة عليه، ويظل رصيد المورد مفتوحًا. |
| F4 | تصنيف المصروفات لا يميّز بين مصروف إنتاج غير مباشر وإداري وبيعي وتمويلي. | لا يمكن حساب وعاء التكاليف غير المباشرة الفعلي، ولا قائمة دخل إدارية صحيحة. |
| F5 | لا يوجد مصروف مباشر على أمر شغل (شحن حالة، توصيل خاص، خدمة لمرة واحدة بلا فاتورة مورد). | تكلفة أمر الشغل ناقصة، وتظهر كمصروف عام. |
| F6 | العمالة بالساعة فقط. معامل كثيرة تحاسب الفني **بالقطعة** (لكل وحدة أو سن) أو براتب ثابت. ولا يوجد تاريخ لتغيّر المعدل. | لا يمكن تمثيل الأجر الفعلي، وتعديل المعدل يغيّر تكلفة الحالات التي لم تُغلق بأثر رجعي. |
| F7 | `OverheadRuleDefinition` تحمل معدلًا يدويًا فقط: لا موازنة ولا مقارنة بالفعلي ولا معالجة للفرق (تحميل زائد أو ناقص) في نهاية الفترة. | الربحية تعتمد على رقم تخميني لا يُراجع، ولا يطابق مجموع المحمَّل ما صُرف فعلًا. |
| F8 | لا يوجد سجل أصول ثابتة ولا إهلاك (أجهزة الخراطة CAD/CAM، الأفران، الطابعات ثلاثية الأبعاد). | أكبر بند تكلفة غير مباشرة في معمل حديث غائب عن الوعاء وعن قائمة الدخل. |

تقارير التكلفة في المرحلة 10 مكتوبة "تقارير التكلفة" دون شاشات محددة، وتُحدَّد في القسم 4.

## 3. الكيانات المقترحة

كل الكيانات `TenantEntity` وتتبع قواعد A2.1 (لا تعديل بعد الترحيل، التصحيح بحركة عكسية، `ClientOperationId`).

### 3.1 الدفتر المساعد للأطراف (F1)

```csharp
namespace DentalLabOS.Domain.Ledger;

public enum PartyType { Customer, Supplier }

/// حركة واحدة على حساب طرف. تُكتب في نفس معاملة ترحيل المستند المصدر ولا تُعدَّل.
public class PartyLedgerEntry : TenantEntity
{
    public PartyType PartyType { get; set; }
    public Guid PartyId { get; set; }                     // CustomerId أو SupplierId
    public Guid? BranchId { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly? DueDate { get; set; }                // للأعمار
    public string SourceType { get; set; } = default!;    // SalesInvoice | CreditNote | Payment | SupplierInvoice | PurchaseReturn | Expense | OpeningBalance | Reversal
    public Guid SourceId { get; set; }
    public string DocumentNumber { get; set; } = default!;
    public decimal Debit { get; set; }                    // بعملة الأساس
    public decimal Credit { get; set; }
    public string CurrencyCode { get; set; } = "EGP";
    public decimal ForeignAmount { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public Guid? ReversalOfEntryId { get; set; }
    public string? Description { get; set; }
}
```

- اتجاه الرصيد: العميل مدين بالفاتورة ودائن بالقبض والإشعار الدائن. المورد دائن بالفاتورة والمصروف الآجل ومدين بالصرف والمرتجع.
- الرصيد = مجموع الحركات. فهرس `(TenantId, PartyType, PartyId, Date)` وفهرس فريد `(TenantId, SourceType, SourceId, PartyId)` يمنع الترحيل المزدوج.
- الأعمار تُحسب من الحركات المدينة غير المخصصة مع `DueDate`، والتخصيص من `PaymentAllocation`.
- في المرحلة 11 يُضاف اختبار مطابقة: مجموع الدفتر المساعد = رصيد حساب الذمم المراقب في الأستاذ العام.

### 3.2 الأرصدة الافتتاحية (F2)

```csharp
public class PartyOpeningBalance : DocumentEntity       // مستند يُرحَّل للدفتر المساعد
{
    public PartyType PartyType { get; set; }
    public Guid PartyId { get; set; }
    public DateOnly AsOfDate { get; set; }
    public ICollection<PartyOpeningBalanceLine> Lines { get; set; } = new List<PartyOpeningBalanceLine>();
}
public class PartyOpeningBalanceLine : TenantEntity     // سطر لكل فاتورة مفتوحة، أو سطر واحد للإجمالي
{
    public Guid PartyOpeningBalanceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateOnly DocumentDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }                   // موجب = على الطرف لصالح المعمل (للعميل) أو للطرف (للمورد)
}
```

- حقل `OpeningBalance` الحالي في `Customer` و`Supplier` يصبح للقراءة فقط بعد ترحيل المستند، ويُهاجَر ما فيه إلى مستند افتتاحي بسطر واحد بتاريخ بداية التشغيل.
- في المرحلة 11 يُنشئ معالج ACC-10 قيد الأستاذ العام المقابل من هذه المستندات، فلا يُدخل الرصيد مرتين.

### 3.3 المصروفات (F3، F4، F5)

```csharp
public enum CostClass { ProductionOverhead, Administrative, Selling, Financial, DirectWorkOrder }

// إضافات على B11
public class ExpenseCategory { /* ... */ public CostClass CostClass { get; set; } public Guid? DefaultDepartmentId { get; set; } }
public class Expense        { /* ... */ public Guid? WorkOrderId { get; set; } public Guid? DepartmentId { get; set; } public DateOnly? DueDate { get; set; } }

// إضافة على B10: التخصيص على هدف عام بدل عمودين ثابتين
public class PaymentAllocation { /* ... */ public string TargetType { get; set; } = default!; public Guid TargetId { get; set; } }
// TargetType: SalesInvoice | SupplierInvoice | Expense | OpeningBalanceLine
```

- `CostClass = DirectWorkOrder` يفرض `WorkOrderId`، والمصروف يُنشئ `WorkOrderCostLine` من نوع `DirectExpense`.
- `CostClass = ProductionOverhead` يدخل وعاء التكاليف غير المباشرة للقسم (`DepartmentId`) في الفترة.
- المصروف الآجل يُكتب في الدفتر المساعد للمورد بتاريخ استحقاق، ويقبل التخصيص من سند الصرف.

### 3.4 العمالة المباشرة (F6)

```csharp
public enum LaborRateBasis { PerHour, PerUnit, Salaried }

public class LaborRate : TenantEntity                    // بديل تاريخي لـ TechnicianProfile.LaborRatePerHour
{
    public Guid? TechnicianProfileId { get; set; }        // فارغ = معدل القسم
    public Guid? DepartmentId { get; set; }
    public Guid? WorkflowStageDefinitionId { get; set; }  // للأجر بالقطعة لكل مرحلة
    public LaborRateBasis Basis { get; set; }
    [SensitiveData] public decimal Rate { get; set; }     // للساعة أو للوحدة
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}
```

- أولوية المعدل: الفني والمرحلة ← الفني ← القسم والمرحلة ← القسم. المعدل يُنسخ في لحظة التسجيل (كما يفعل `LaborRateSnapshot` الآن) ولا يتغير بأثر رجعي.
- بالساعة: الدقائق الفعلية من `TaskTimeEntry` × المعدل. بالقطعة: عدد الوحدات (الأسنان) عند إكمال المرحلة × المعدل. براتب: لا تكلفة مباشرة للحالة، ويدخل الراتب وعاء التكاليف غير المباشرة للقسم، ويُوزَّع بالساعات.
- الحقل الحالي `LaborRatePerHour` يُهاجَر إلى سجل `LaborRate` أول بتاريخ بداية التشغيل.

### 3.5 التكاليف غير المباشرة (F7)

```csharp
public enum OverheadBasis { PerLaborHour, PerUnit, PercentOfDirectCost, PerMachineHour }

public class OverheadBudget : TenantEntity             // موازنة سنوية أو لكل فترة لكل قسم
{
    public DateOnly PeriodStart { get; set; }             // الفترات المحاسبية لا توجد قبل المرحلة 11
    public DateOnly PeriodEnd { get; set; }
    public Guid? DepartmentId { get; set; }
    public decimal BudgetedAmount { get; set; }
    public decimal BudgetedBaseQuantity { get; set; }     // ساعات أو وحدات متوقعة
    // المعدل المحدد مسبقًا = BudgetedAmount / BudgetedBaseQuantity، يُنسخ في OverheadRuleDefinition بعد الاعتماد
}

public class OverheadPeriodRun : TenantEntity          // إقفال التكاليف غير المباشرة لفترة
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid? DepartmentId { get; set; }
    public decimal ActualPool { get; set; }               // مصروفات ProductionOverhead + إهلاك + رواتب غير مباشرة
    public decimal AppliedAmount { get; set; }            // مجموع WorkOrderCostLine من نوع Overhead
    public decimal Variance => ActualPool - AppliedAmount; // موجب = تحميل ناقص
    public string VarianceTreatment { get; set; } = "Expense"; // Expense (لتكلفة المبيعات) | Reallocate (على أوامر الفترة)
    public string Status { get; set; } = "Draft";         // Draft | Approved | Posted
    public Guid? ApprovedByUserId { get; set; }
}
```

- التحميل خلال الفترة بالمعدل المحدد مسبقًا، والمقارنة بالفعلي في نهاية الفترة، والفرق يُعتمد ويُعالج حسب القرار، ثم يُرحَّل في المرحلة 11.
- `PerMachineHour` يُترك معطلًا حتى يوجد تسجيل لوقت الأجهزة (خارج النطاق الحالي).

### 3.6 الأصول الثابتة والإهلاك (F8)

```csharp
public class FixedAsset : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid? DepartmentId { get; set; }
    public DateOnly AcquisitionDate { get; set; }
    public decimal Cost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeMonths { get; set; }
    public string Method { get; set; } = "StraightLine";
    public Guid? SupplierInvoiceId { get; set; }
    public bool IsProductionAsset { get; set; }           // يدخل وعاء التكاليف غير المباشرة
    public DateOnly? DisposedOn { get; set; }
}
public class DepreciationEntry : TenantEntity { public Guid FixedAssetId { get; set; } public DateOnly PeriodEnd { get; set; } public decimal Amount { get; set; } public string Status { get; set; } = "Draft"; }
```

- نطاق مقصود ضيق: قسط ثابت، مسودة شهرية تُراجع ولا تُرحَّل آليًا (مثل المصروفات المتكررة). لا إعادة تقييم ولا بيع جزئي.

## 4. الشاشات الجديدة

| ID | الشاشة | المسار | المفتاح | المرحلة |
|---|---|---|---|---|
| SUP-07 | الرصيد الافتتاحي للمورد (بالفواتير المفتوحة) | /suppliers/{id}/opening | suppliers.opening.manage | 6 |
| CUS-10 | الرصيد الافتتاحي للعميل (بالفواتير المفتوحة) | /customers/{id}/opening | customers.opening.manage | 7 |
| CUS-11 | استيراد الأرصدة الافتتاحية من Excel (عملاء وموردون) | /admin/import/opening-balances | admin.import.manage | 7 |
| PAY-11 | تخصيص سند صرف على مصروفات آجلة | ضمن PAY-05 | payments.allocate | 9 |
| COST-01 | بطاقة تكلفة أمر الشغل (تبويب في ORD-07) | /work-orders/{id}/cost | costing.workorder.read | 10 |
| COST-02 | معدلات العمالة (بالساعة/بالقطعة/راتب، بتواريخ) | /admin/labor-rates | admin.costing.manage | 10 |
| COST-03 | موازنة التكاليف غير المباشرة والمعدل المحدد مسبقًا | /costing/overhead-budget | admin.costing.manage | 10 |
| COST-04 | إقفال التكاليف غير المباشرة (الفعلي مقابل المحمَّل والفرق) | /costing/overhead-close | costing.overhead.close | 10 |
| COST-05 | انحراف التكلفة (المعياري من BOM مقابل الفعلي) | /costing/variance | reports.profit.read | 10 |
| RPT-13 | قائمة دخل إدارية (إيراد − تكلفة مباشرة − مصروفات بحسب CostClass) | /reports/management-pl | reports.profit.read | 10 |
| ACC-20 | سجل الأصول الثابتة | /accounting/assets | accounting.assets.manage | 11 |
| ACC-21 | مسودات الإهلاك الشهرية | /accounting/depreciation | accounting.assets.manage | 11 |

RPT-13 تعمل قبل المحاسبة من الدفتر المساعد والمصروفات وبطاقات التكلفة، وتُعلَّم "إدارية وليست قائمة مالية رسمية" حتى المرحلة 11.

مفاتيح صلاحيات جديدة: `suppliers.opening.manage`، `customers.opening.manage`، `costing.workorder.read`، `costing.overhead.close`، `accounting.assets.manage`. التكلفة تبقى مخفية عن من لا يملك `inventory.valuation.read` أو `costing.workorder.read`، والمعدلات `[SensitiveData]`.

## 5. التوزيع على المراحل

| المرحلة | الإضافة | شرط قبول إضافي |
|---|---|---|
| 4 (قيد البناء) | لا تغيير في الكود. يكفي أن يبقى ربط إعادة التصنيع بالأصل وتصنيف السبب، ويحسم قرار تحميل التكلفة لاحقًا في المرحلة 10. | — |
| 5 المخزون | لا إضافة. حركة `IssueToWorkOrder` بتكلفة الوحدة كما هي مخططة. | — |
| 6 المشتريات | بناء `PartyLedgerEntry` (البنية والفهارس والاستعلامات المشتركة للكشف والأعمار). ترحيل فاتورة المورد والمرتجع للدفتر. SUP-04 وPUR-10 منه. SUP-07 ومهاجرة الرصيد الافتتاحي للموردين. | رصيد المورد في SUP-03 وSUP-04 وPUR-10 واحد في كل الحالات المختبَرة |
| 7 المبيعات | ترحيل الفاتورة والإشعار الدائن للدفتر. CUS-06 وCUS-07 وSAL-10 منه. CUS-10 وCUS-11 ومهاجرة الرصيد الافتتاحي للعملاء. فحص حد الائتمان على رصيد الدفتر + الفواتير المسودة. | كشف الحساب يطابق مجموع المستندات، والأعمار صحيحة مع رصيد افتتاحي مفصّل |
| 8 المدفوعات | ترحيل السندات للدفتر. `PaymentAllocation` بهدف عام (`TargetType/TargetId`) من البداية. | تخصيص صحيح على فاتورة ورصيد افتتاحي |
| 9 المصروفات | `CostClass` و`DepartmentId` و`WorkOrderId` و`DueDate`. المصروف الآجل للدفتر المساعد للمورد. PAY-11. | مصروف آجل يُسدَّد ويُغلق رصيد المورد |
| 10 التكلفة | `LaborRate` بالأنواع الثلاثة ومهاجرة المعدل الحالي. `OverheadBudget` و`OverheadPeriodRun`. سطر تكلفة `DirectExpense`. COST-01..05 وRPT-13. قرار تحميل إعادة التصنيع. | مجموع المحمَّل + الفرق = الوعاء الفعلي، وتكلفة أمر الشغل = مجموع مصادرها |
| 11 المحاسبة | ACC-10 يقرأ مستندات الأرصدة الافتتاحية. مطابقة الدفتر المساعد بحسابات الذمم المراقبة. ترحيل فرق التكاليف غير المباشرة. ACC-20/21 الأصول والإهلاك. | الدفتر المساعد = حساب المراقبة، وميزان متوازن |

قبل المرحلة 11 يُضاف الإهلاك يدويًا كمصروف متكرر بتصنيف `ProductionOverhead` حتى لا يغيب عن وعاء التكاليف.

## 6. قرارات مطلوبة من صاحب المشروع

| القرار | الافتراض المقترح إن لم يُحسم | مطلوب قبل |
|---|---|---|
| الرصيد الافتتاحي: إجمالي واحد أم مفصّل بالفواتير المفتوحة | مفصّل اختياريًا، ويقبل سطرًا واحدًا | المرحلة 6 |
| طريقة أجر الفنيين السائدة (ساعة، قطعة، راتب) | الثلاثة مدعومة، والافتراضي بالساعة | المرحلة 10 |
| أساس توزيع التكاليف غير المباشرة | ساعات العمل المباشرة لكل قسم | المرحلة 10 |
| معالجة فرق التحميل في نهاية الفترة | يُحمَّل على تكلفة المبيعات للفترة | المرحلة 10 |
| هل يدخل سجل الأصول والإهلاك في الإصدار الأول | نعم بنطاق ضيق في المرحلة 11، ومصروف متكرر قبلها | المرحلة 11 |

هذه القرارات وطريقة التوزيع تحتاج مراجعة محاسب قبل البرمجة، كما ينص القسم 5.8 من الخطة.
