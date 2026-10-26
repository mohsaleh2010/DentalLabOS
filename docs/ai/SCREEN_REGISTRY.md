# سجل الشاشات (Screen Registry)

> ملف مولَّد آليًا من `docs/screens/*.screen.json` بواسطة `python3 tools/screens/generate.py`. لا تعدّله يدويًا.

عدد الشاشات الموثقة: **27**. اختبارات المعمارية تفشل إذا وُجدت صفحة تحمل `[Screen]` بلا ملف مواصفات، أو ملف يخالف المخطط، أو صلاحية غير معرّفة في الكتالوج.

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
| [ADM-25](../screens/ADM-25.screen.json) | admin | `/admin/audit` | ListPage | `admin.audit.read` | implemented |
| [ADM-26](../screens/ADM-26.screen.json) | admin | `/admin/subscription` | DashboardPage | `admin.subscription.read` | implemented |
| [AUTH-01](../screens/AUTH-01.screen.json) | auth | `/login` | AuthPage | `anonymous` | implemented |
| [AUTH-05](../screens/AUTH-05.screen.json) | auth | `/invite/accept` | AuthPage | `anonymous` | implemented |
| [AUTH-06](../screens/AUTH-06.screen.json) | auth | `/setup` | AuthPage | `anonymous` | implemented |
| [AUTH-07](../screens/AUTH-07.screen.json) | auth | `/account/password` | FormPage | `authenticated` | implemented |
| [AUTH-09](../screens/AUTH-09.screen.json) | auth | `/session-expired` | StatusPage | `anonymous` | implemented |
| [NTF-01](../screens/NTF-01.screen.json) | notifications | `/notifications` | ListPage | `authenticated` | implemented |
| [PLT-01](../screens/PLT-01.screen.json) | platform | `/platform` | DashboardPage | `platform.dashboard.read` | implemented |
| [PLT-02](../screens/PLT-02.screen.json) | platform | `/platform/tenants` | ListPage | `platform.tenants.read` | implemented |
| [PLT-03](../screens/PLT-03.screen.json) | platform | `/platform/tenants/new` | FormPage | `platform.tenants.create` | implemented |
| [PLT-04](../screens/PLT-04.screen.json) | platform | `/platform/tenants/{id}` | DetailsTabs | `platform.tenants.read` | implemented |
| [PLT-06](../screens/PLT-06.screen.json) | platform | `/platform/plans` | ListPage | `platform.tenants.read` | implemented |
| [PLT-12](../screens/PLT-12.screen.json) | platform | `/platform/audit` | ListPage | `platform.audit.read` | implemented |
| [SYS-01](../screens/SYS-01.screen.json) | system | `/account` | DetailsTabs | `authenticated` | implemented |
| [SYS-02](../screens/SYS-02.screen.json) | system | `/account/preferences` | FormPage | `authenticated` | implemented |
| [SYS-05](../screens/SYS-05.screen.json) | system | `/search` | ListPage | `authenticated` | implemented |
| [SYS-06](../screens/SYS-06.screen.json) | system | `/offline` | StatusPage | `anonymous` | implemented |
| [SYS-07](../screens/SYS-07.screen.json) | system | `/error/{code}` | StatusPage | `anonymous` | implemented |
