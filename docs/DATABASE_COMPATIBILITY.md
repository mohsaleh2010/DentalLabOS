# توافق قاعدة البيانات

- المحرك المعتمد: **SQL Server 2019+ / Azure SQL**. الاختبارات والـ CI على SQL Server 2022.
- الـ Migrations في `src/DentalLabOS.Infrastructure/Persistence/Migrations`، وCI يفشل إذا تغيّر النموذج دون migration (`has-pending-model-changes`).
- في الإنتاج لا تُطبَّق الـ migrations تلقائيًا (`Database:ApplyMigrationsOnStartup=false`)؛ تُطبَّق كخطوة نشر منفصلة بسكربت idempotent: `dotnet ef migrations script --idempotent`.

## اصطلاحات

| البند | القاعدة |
|---|---|
| المعرّفات | `Guid` بإصدار 7 (مرتبة زمنيًا لتقليل تجزؤ الفهارس) |
| التعارض | `rowversion` على كل كيان، ويُرسل للواجهة كـ Base64 |
| المبالغ والكميات | `decimal(18,4)` افتراضيًا |
| النصوص | `nvarchar(256)` افتراضيًا ما لم يُحدَّد غير ذلك |
| التعدادات | تُخزن كنص (حتى 32 حرفًا) لسهولة القراءة والتطوير |
| الحذف | ناعم (`IsDeleted`) للمستندات والبيانات الرئيسية، والسجلات المالية لا تُحذف أبدًا |
| الترقيم | `UPDATE ... WITH (UPDLOCK, ROWLOCK) OUTPUT` لضمان التسلسل دون فجوات تحت التزامن |

## SQL خام

مسموح فقط في `NumberingService` حاليًا، وبمعاملات (parameters) دائمًا. أي استخدام جديد يحتاج مراجعة لتوافق المحرك والعزل.
