# دليل وكلاء البناء (AI Build Guide)

تعليمات إلزامية لأي وكيل ذكاء اصطناعي (أو مطور) يبني شاشة أو وحدة في DentalLab OS. مأخوذة من القسم C4 في الخطة.

## قبل كتابة أي كود
1. اقرأ `docs/screens/{ID}.screen.json`، و`docs/TENANT_ISOLATION.md`، ومعايير القبول للمرحلة في `docs/EXECUTION_PLAN.md`.
2. لا تخمّن حقولًا أو صلاحيات غير موثقة. إن نقص شيء سجّله كسؤال مفتوح في الـ PR ولا تخترعه.
3. إن لم يوجد ملف المواصفات: اكتبه أولًا وفق `docs/screens/_schema/screen.schema.json`.

## ترتيب البناء
1. الكيان في `Domain` (يرث `TenantEntity` إن كان تابعًا لمعمل) + التهيئة في `Infrastructure/Persistence/Configurations` + migration.
2. الخدمة في `Application`: تتحقق من الصلاحية (`permissions.DemandAsync`) والمدخلات (`Validator`) وقواعد المجال.
3. DTOs في `Contracts` وendpoint في `Api/Endpoints` بـ `RequireAuthorization(PermissionPolicy.For(...))`.
4. الصفحة في `Web/Pages` ترث `AppPageModel` وتحمل `[Screen("ID")]` و`[HasPermission(...)]`، وتستدعي الخدمة فقط.
5. النصوص في `tools/i18n/strings.py` (عربي + إنجليزي) ثم `python3 tools/i18n/build.py`.
6. الاختبارات: لكل قاعدة في `rules` اختبار، ولكل شاشة: `permissionDenied`، `crossTenant`، تعارض RowVersion، وidempotency للعمليات المالية/المخزنية.
7. `python3 tools/screens/generate.py` لتحديث الفهرس، وحدّث `PROJECT_STATUS.md` و`CHANGELOG.md`.

## ممنوع
- قواعد عمل داخل PageModel أو المكونات.
- `IgnoreQueryFilters` خارج القائمة المراجَعة، أو تمرير `TenantId` من العميل.
- أسرار أو سلاسل اتصال أو كلمات مرور في المستودع.
- تعديل أو حذف حركات مرحَّلة؛ التصحيح يكون بحركة عكسية.
- الربط بحسابات محاسبية مباشرة؛ استخدم `AccountMapping` عبر خدمة الترحيل.
- إرسال بيانات مرضى أو هوية لمزود خارجي.
- إعلان اكتمال قبل نجاح كل الاختبارات في CI.

## أنماط الشاشات
`ListPage`، `FormPage`، `DetailsTabs`، `ActionDialog`، `ApprovalQueue`، `ReportPage`، `DashboardPage`. استخدم مكونات وأصناف CSS الموجودة في `SharedUI` (`.card`، `.table`، `.toolbar`، `.field`، `.badge`، `.matrix`...) ولا تنسخ شاشة قديمة.
