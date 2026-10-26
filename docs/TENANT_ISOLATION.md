# العزل بين المعامل (Tenant Isolation)

كل معمل مستأجر مستقل. لا يجوز أن يقرأ أو يعدّل مستخدم أي بيانات تخص معملًا آخر، ولا أن يعرف بوجودها.

## الطبقات

| الطبقة | الآلية | الملف |
|---|---|---|
| مصدر هوية المعمل | المطالبة `dlos:tid` في الكوكي أو الـ JWT، ولا تُقبل من الطلب أبدًا | `Infrastructure/Tenancy/CurrentUser.cs` |
| القراءة | مرشح استعلام عام على كل كيان `ITenantOwned`: `TenantId == CurrentTenantFilterId`. عند غياب السياق تكون القيمة `Guid.Empty` فلا يعود أي صف (Fail-closed) | `Persistence/AppDbContext.cs` |
| الكتابة | `TenantSaveChangesInterceptor`: يختم `TenantId` على الإضافات، ويرمي `TenantIsolationException` عند إضافة أو تعديل أو حذف صف لمعمل آخر، ويمنع تغيير `TenantId` | `Persistence/TenantSaveChangesInterceptor.cs` |
| الصلاحيات | تُحسب لكل طلب من قاعدة البيانات بأدوار المستخدم داخل معمله فقط؛ موظفو المنصة لا يملكون صلاحيات معامل | `Security/PermissionService.cs` |
| الجلسة | إيقاف المستخدم أو المعمل يُنهي الجلسة/التوكن في الطلب التالي | `AccessStateMiddleware` (Web وApi) |
| الفهارس | كل قيد فريد يبدأ بـ `TenantId` (مثل كود الفرع) | `Persistence/Configurations/*` |
| سجل التدقيق | الصفوف ذات `TenantId = null` خاصة بالمنصة ولا تظهر لأي معمل | `AppDbContext` |

## تجاوز المرشحات

`IgnoreQueryFilters()` مسموح فقط في ملفات مراجَعة ومسجلة في `tests/DentalLabOS.ArchitectureTests/LayeringTests.cs`:

- `PlatformTenantService` — بيانات الاشتراك فقط لموظفي المنصة.
- `InvitationService` — البحث عن الدعوة ببصمة التوكن قبل الدخول، ثم العمل داخل نطاق المعمل.
- `AuditQueryService` — سجل المنصة (`TenantId == null`) فقط.

أي استخدام جديد يفشل CI حتى يُراجَع ويُضاف للقائمة مع مبرر.

## قواعد للمطورين

1. كل كيان تابع لمعمل يرث `TenantEntity` (اختبار معمارية يتحقق).
2. لا تمرر `TenantId` من الواجهة أو الـ API؛ الخادم يحدده.
3. أعد 404 وليس 403 عند طلب سجل معمل آخر (لا تكشف وجوده) — المرشح يفعل ذلك تلقائيًا.
4. العمليات التي تُنشئ بيانات لمعمل آخر (مثل إنشاء معمل من المنصة) تستخدم `ITenantContext.BeginScope(tenantId)` بشكل صريح.
5. كل وحدة جديدة تضيف اختبار تكامل `crossTenant` (انظر `FoundationTests.Tenant_isolation_*`).
