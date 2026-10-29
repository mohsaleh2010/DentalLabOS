# DentalLab OS

نظام سحابي (SaaS) متعدد المعامل لإدارة معامل الأسنان: الاستلام، أوامر الشغل، الإنتاج، الجودة، التسليم، المخزون، المشتريات، الفوترة، الخزينة والمحاسبة. عربي أولًا (RTL) مع دعم الإنجليزية.

> الحالة الحالية: **المرحلة 4 (الجودة وإعادة التصنيع والتسليم)** بعد الأساس والأطراف والأصناف والطلبيات والإنتاج. التفاصيل في [PROJECT_STATUS.md](PROJECT_STATUS.md) وخطة التنفيذ الكاملة في [docs/EXECUTION_PLAN.md](docs/EXECUTION_PLAN.md).

## التقنيات

| الطبقة | التقنية |
|---|---|
| المنصة | .NET 10 LTS |
| الواجهة | ASP.NET Core Razor Pages + مكونات Razor مشتركة (`SharedUI`) + PWA |
| الواجهة البرمجية | Minimal API تحت `/api/v1` مع OpenAPI وScalar |
| البيانات | SQL Server + EF Core 10 (Migrations) |
| الهوية | ASP.NET Core Identity (كوكي للويب، JWT قصير العمر للـ API) |
| المعمارية | Modular Monolith: `Domain` ← `Application` ← `Infrastructure` ← `Web`/`Api` |

## هيكل المستودع

```text
src/
  DentalLabOS.Domain          الكيانات وقواعد المجال (بلا أي اعتماد على أطر)
  DentalLabOS.Contracts       DTOs مشتركة بين الويب والـ API والعملاء
  DentalLabOS.Application     خدمات حالات الاستخدام + التحقق + فحص الصلاحيات
  DentalLabOS.Infrastructure  EF Core، العزل بين المعامل، الهوية، التدقيق، الترقيم
  DentalLabOS.SharedUI        نظام التصميم: CSS، أيقونات، خطوط، مكونات، ملفات الترجمة
  DentalLabOS.Web             تطبيق الويب (Razor Pages)
  DentalLabOS.Api             REST API v1
tests/
  DentalLabOS.UnitTests          قواعد المجال والكتالوج
  DentalLabOS.IntegrationTests   على SQL Server حقيقي (عزل، صلاحيات، تعارض، ترقيم)
  DentalLabOS.ArchitectureTests  الطبقات، سجل الشاشات، الترجمة، منع الأسرار
docs/
  screens/*.screen.json       مواصفات كل شاشة (المصدر الوحيد للحقيقة)
  ai/                         سجل الشاشات ودليل وكلاء البناء وسياسة سياق المساعد
  adr/                        قرارات معمارية
tools/                        مولّد ملفات الترجمة وفهرس الشاشات
```

## التشغيل محليًا

المتطلبات: .NET SDK 10، وSQL Server (محلي أو Docker).

```bash
# 1) قاعدة بيانات للتطوير (مثال Docker) — اختر كلمة مرور خاصة بك
docker run -d --name dlos-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='<your-strong-password>' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

# 2) سلسلة الاتصال عبر user-secrets (لا تُحفظ في Git أبدًا)
dotnet user-secrets --project src/DentalLabOS.Web set "ConnectionStrings:DentalLabOS" "Server=localhost,1433;Database=DentalLabOS_Dev;User Id=sa;Password=<your-strong-password>;TrustServerCertificate=true"

# 3) تشغيل الويب (يطبّق الـ migrations تلقائيًا في Development)
dotnet run --project src/DentalLabOS.Web
```

**التهيئة الأولى:** لا يوجد أي حساب أو كلمة مرور افتراضية. عند أول تشغيل يطبع الخادم في السجل **رمز تهيئة** لمرة واحدة (أو اضبطه بنفسك في `Bootstrap:SetupToken`). افتح `/setup` وأنشئ حساب مالك المنصة، ثم أنشئ أول معمل من بوابة المنصة وأرسل رابط الدعوة لمالكه.

**الـ API:**

```bash
dotnet user-secrets --project src/DentalLabOS.Api set "ConnectionStrings:DentalLabOS" "<same connection string>"
dotnet user-secrets --project src/DentalLabOS.Api set "Jwt:SigningKey" "<random 32+ byte secret>"
dotnet run --project src/DentalLabOS.Api   # التوثيق التفاعلي على /scalar/v1 في Development
```

## الاختبارات

```bash
dotnet test tests/DentalLabOS.UnitTests
dotnet test tests/DentalLabOS.ArchitectureTests
DLOS_TEST_SQL="Server=localhost,1433;User Id=sa;Password=<pwd>;TrustServerCertificate=true" dotnet test tests/DentalLabOS.IntegrationTests
```

اختبارات التكامل تنشئ قاعدة بيانات مؤقتة باسم عشوائي وتحذفها بعد الانتهاء. لا تُستخدم قاعدة بيانات في الذاكرة لأن العزل والأقفال والتعارض يجب أن تُختبر على المحرك الحقيقي.

## قواعد غير قابلة للتفاوض

1. لا أسرار ولا كلمات مرور ولا سلاسل اتصال في Git (اختبار معمارية يفشل إن وُجدت).
2. لا حسابات أو كلمات مرور افتراضية ثابتة.
3. العزل بين المعامل يُفرض على الخادم (مرشحات استعلام + حارس كتابة)، وانظر [docs/TENANT_ISOLATION.md](docs/TENANT_ISOLATION.md).
4. إخفاء زر في الواجهة ليس أمانًا: كل صفحة وendpoint يفحص الصلاحية على الخادم في كل طلب.
5. لا تكتمل مرحلة قبل نجاح اختباراتها.

## إضافة نص أو شاشة

- **نص جديد:** أضفه في `tools/i18n/strings.py` (عربي + إنجليزي) ثم `python3 tools/i18n/build.py`.
- **شاشة جديدة:** اكتب `docs/screens/{ID}.screen.json` أولًا، ثم الصفحة مع `[Screen("ID")]` و`[HasPermission(...)]`، ثم `python3 tools/screens/generate.py` لتحديث الفهرس. راجع [docs/ai/AI_BUILD_GUIDE.md](docs/ai/AI_BUILD_GUIDE.md).
