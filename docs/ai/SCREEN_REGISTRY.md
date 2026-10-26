# سجل الشاشات (Screen Registry)

> ملف مولَّد آليًا من `docs/screens/*.screen.json` بواسطة `python3 tools/screens/generate.py`. لا تعدّله يدويًا.

عدد الشاشات الموثقة: **57**. اختبارات المعمارية تفشل إذا وُجدت صفحة تحمل `[Screen]` بلا ملف مواصفات، أو ملف يخالف المخطط، أو صلاحية غير معرّفة في الكتالوج.

| ID | الوحدة | المسار | النمط | صلاحية العرض | الحالة |
|---|---|---|---|---|---|
| [ADM-01](../screens/ADM-01.screen.json) | admin | `/admin` | DashboardPage | `admin.dashboard.read` | implemented |
| [ADM-03](../screens/ADM-03.screen.json) | admin | `/admin/branches` | ListPage | `admin.branches.manage` | implemented |
| [ADM-04](../screens/ADM-04.screen.json) | admin | `/admin/departments` | ListPage | `admin.departments.manage` | implemented |
| [ADM-05](../screens/ADM-05.screen.json) | admin | `/admin/users` | ListPage | `admin.users.read` | implemented |
| [ADM-06](../screens/ADM-06.screen.json) | admin | `/admin/users/{id}` | DetailsTabs | `admin.users.read` | implemented |
| [ADM-08](../screens/ADM-08.screen.json) | admin | `/admin/roles` | ListPage | `admin.roles.manage` | implemented |
| [ADM-09](../screens/ADM-09.screen.json) | admin | `/admin/roles/{id}/permissions` | Custom | `admin.roles.manage` | implemented |
| [ADM-11](../screens/ADM-11.screen.json) | admin | `/admin/numbering` | Custom | `admin.numbering.manage` | implemented |
| [ADM-12](../screens/ADM-12.screen.json) | admin | `/admin/taxes` | Custom | `admin.taxes.manage` | implemented |
| [ADM-13](../screens/ADM-13.screen.json) | admin | `/admin/currencies` | Custom | `admin.currencies.manage` | implemented |
| [ADM-14](../screens/ADM-14.screen.json) | admin | `/admin/units` | Custom | `admin.units.manage` | implemented |
| [ADM-23](../screens/ADM-23.screen.json) | admin | `/admin/import` | Custom | `admin.import.manage` | implemented |
| [ADM-25](../screens/ADM-25.screen.json) | admin | `/admin/audit` | ListPage | `admin.audit.read` | implemented |
| [ADM-26](../screens/ADM-26.screen.json) | admin | `/admin/subscription` | DashboardPage | `admin.subscription.read` | implemented |
| [AUTH-01](../screens/AUTH-01.screen.json) | auth | `/login` | AuthPage | `anonymous` | implemented |
| [AUTH-05](../screens/AUTH-05.screen.json) | auth | `/invite/accept` | AuthPage | `anonymous` | implemented |
| [AUTH-06](../screens/AUTH-06.screen.json) | auth | `/setup` | AuthPage | `anonymous` | implemented |
| [AUTH-07](../screens/AUTH-07.screen.json) | auth | `/account/password` | FormPage | `authenticated` | implemented |
| [AUTH-09](../screens/AUTH-09.screen.json) | auth | `/session-expired` | StatusPage | `anonymous` | implemented |
| [CUS-01](../screens/CUS-01.screen.json) | customers | `/customers` | ListPage | `customers.read` | implemented |
| [CUS-02](../screens/CUS-02.screen.json) | customers | `/customers/{id}/edit` | FormPage | `customers.manage` | implemented |
| [CUS-03](../screens/CUS-03.screen.json) | customers | `/customers/{id}` | DetailsTabs | `customers.read` | implemented |
| [CUS-04](../screens/CUS-04.screen.json) | customers | `/customers/{id}/dentists` | Custom | `customers.dentists.manage` | implemented |
| [CUS-05](../screens/CUS-05.screen.json) | customers | `/customers/groups` | Custom | `customers.groups.manage` | implemented |
| [CUS-06](../screens/CUS-06.screen.json) | customers | `/customers/{id}/statement` | Custom | `customers.statement.read` | planned |
| [CUS-07](../screens/CUS-07.screen.json) | customers | `/customers/{id}/credit` | Custom | `customers.credit.manage` | planned |
| [CUS-08](../screens/CUS-08.screen.json) | customers | `/customers/{id}/portal-users` | Custom | `customers.portal.manage` | planned |
| [CUS-09](../screens/CUS-09.screen.json) | customers | `/customers/merge` | Custom | `customers.merge` | planned |
| [ITM-01](../screens/ITM-01.screen.json) | items | `/items` | ListPage | `items.read` | implemented |
| [ITM-02](../screens/ITM-02.screen.json) | items | `/items/{id}/edit` | FormPage | `items.manage` | implemented |
| [ITM-03](../screens/ITM-03.screen.json) | items | `/items/{id}` | DetailsTabs | `items.read` | implemented |
| [ITM-04](../screens/ITM-04.screen.json) | items | `/items/categories` | Custom | `items.categories.manage` | implemented |
| [ITM-05](../screens/ITM-05.screen.json) | items | `/catalog/services` | Custom | `items.services.read` | implemented |
| [ITM-06](../screens/ITM-06.screen.json) | items | `/items/{id}/bom` | Custom | `items.bom.manage` | implemented |
| [ITM-07](../screens/ITM-07.screen.json) | items | `/price-lists` | ListPage | `pricelists.read` | implemented |
| [ITM-08](../screens/ITM-08.screen.json) | items | `/price-lists/{id}` | Custom | `pricelists.read` | implemented |
| [ITM-09](../screens/ITM-09.screen.json) | items | `/price-lists/bulk-update` | Custom | `pricelists.bulk` | implemented |
| [ITM-10](../screens/ITM-10.screen.json) | items | `/items/labels` | Custom | `items.labels.print` | planned |
| [ITM-11](../screens/ITM-11.screen.json) | items | `/items/import` | Custom | `items.import` | implemented |
| [NTF-01](../screens/NTF-01.screen.json) | notifications | `/notifications` | ListPage | `authenticated` | implemented |
| [PLT-01](../screens/PLT-01.screen.json) | platform | `/platform` | DashboardPage | `platform.dashboard.read` | implemented |
| [PLT-02](../screens/PLT-02.screen.json) | platform | `/platform/tenants` | ListPage | `platform.tenants.read` | implemented |
| [PLT-03](../screens/PLT-03.screen.json) | platform | `/platform/tenants/new` | FormPage | `platform.tenants.create` | implemented |
| [PLT-04](../screens/PLT-04.screen.json) | platform | `/platform/tenants/{id}` | DetailsTabs | `platform.tenants.read` | implemented |
| [PLT-06](../screens/PLT-06.screen.json) | platform | `/platform/plans` | ListPage | `platform.tenants.read` | implemented |
| [PLT-12](../screens/PLT-12.screen.json) | platform | `/platform/audit` | ListPage | `platform.audit.read` | implemented |
| [SUP-01](../screens/SUP-01.screen.json) | suppliers | `/suppliers` | ListPage | `suppliers.read` | implemented |
| [SUP-02](../screens/SUP-02.screen.json) | suppliers | `/suppliers/{id}/edit` | FormPage | `suppliers.manage` | implemented |
| [SUP-03](../screens/SUP-03.screen.json) | suppliers | `/suppliers/{id}` | DetailsTabs | `suppliers.read` | implemented |
| [SUP-04](../screens/SUP-04.screen.json) | suppliers | `/suppliers/{id}/statement` | Custom | `suppliers.statement.read` | planned |
| [SUP-05](../screens/SUP-05.screen.json) | suppliers | `/suppliers/{id}/items` | Custom | `suppliers.items.manage` | implemented |
| [SUP-06](../screens/SUP-06.screen.json) | suppliers | `/suppliers/{id}/rating` | Custom | `suppliers.rating.manage` | planned |
| [SYS-01](../screens/SYS-01.screen.json) | system | `/account` | DetailsTabs | `authenticated` | implemented |
| [SYS-02](../screens/SYS-02.screen.json) | system | `/account/preferences` | FormPage | `authenticated` | implemented |
| [SYS-05](../screens/SYS-05.screen.json) | system | `/search` | ListPage | `authenticated` | implemented |
| [SYS-06](../screens/SYS-06.screen.json) | system | `/offline` | StatusPage | `anonymous` | implemented |
| [SYS-07](../screens/SYS-07.screen.json) | system | `/error/{code}` | StatusPage | `anonymous` | implemented |
