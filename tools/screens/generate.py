"""Screen registry tooling (plan section C).

  python3 tools/screens/generate.py              rebuild docs/ai/SCREEN_REGISTRY.md from docs/screens/*.screen.json
  python3 tools/screens/generate.py --bootstrap  (re)write the phase 0-1 specs below, then rebuild the index

The JSON files are the source of truth; edit them directly. --bootstrap is only for the initial phase 0-1 set.
"""
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "screens"

NO_AI = {"allowed": [], "forbidden": ["*"], "contextFields": []}
EXPLAIN = lambda *ctx: {"allowed": ["explainScreen"], "forbidden": ["changeData", "changePermissions"], "contextFields": list(ctx)}
ONLINE = {"mode": "blocked", "why": "requires live server-side checks"}


def spec(id, module, route, pattern, view, title, *, actions=None, access=None, entities=(), api=(), fields=None,
         columns=None, filters=None, rules=(), states=("loading",), offline=ONLINE, sensitive=None, ai=None,
         tests=(), related=(), routes=None, notes=None):
    s = {
        "$schema": "./_schema/screen.schema.json",
        "id": id,
        "module": module,
        "titleKey": title,
        "route": route,
        "pattern": pattern,
        "phase": 1,
        "status": "implemented",
        "permissions": {"view": view, "access": access or ("permission" if view else "authenticated")},
        "entities": list(entities),
        "api": [dict(zip(("method", "path", "purpose"), a)) for a in api],
        "rules": list(rules),
        "states": list(states),
        "offline": offline,
        "sensitive": sensitive or {},
        "aiAssist": ai or EXPLAIN(),
        "tests": [f"{id}.{t}" for t in tests] or [f"{id}.renders"],
        "relatedScreens": list(related),
    }
    if actions:
        s["permissions"]["actions"] = actions
    if routes:
        s["routes"] = routes
    if fields is not None:
        s["fields"] = fields
    if columns is not None:
        s["columns"] = columns
    if filters is not None:
        s["filters"] = filters
    if notes:
        s["notes"] = notes
    return s


def f(name, type, required=False, **kw):
    d = {"name": name, "type": type}
    if required:
        d["required"] = True
    d.update(kw)
    return d


TENANT_TESTS = ("happy", "permissionDenied", "crossTenant")

SPECS = [
    # ---- Authentication & system ----
    spec("AUTH-01", "auth", "/login", "AuthPage", None, "Auth.Login.Title", access="anonymous",
         entities=["ApplicationUser", "AuditLog"], api=[("POST", "/api/v1/auth/token", "API sign-in")],
         fields=[f("email", "email", True, maxLength=256), f("password", "password", True), f("rememberMe", "boolean")],
         rules=["One generic message for unknown e-mail and wrong password (no account enumeration)",
                "Lockout after 5 failed attempts for 15 minutes",
                "Deactivated users and users of suspended/closed labs cannot sign in",
                "Every success and failure is written to AuditLog"],
         states=["validationError", "locked"], sensitive={"auditActions": ["Login", "LoginFailed"], "secrets": ["password"]},
         ai=NO_AI, tests=("happy", "badPassword", "lockout", "suspendedTenant"), related=["AUTH-05", "AUTH-06"]),
    spec("AUTH-05", "auth", "/invite/accept", "AuthPage", None, "Auth.Invite.Title", access="anonymous",
         entities=["UserInvitation", "ApplicationUser", "UserProfile", "UserRole"],
         fields=[f("token", "hidden", True), f("password", "password", True), f("confirmPassword", "password", True)],
         rules=["Only the SHA-256 hash of the token is stored", "Expired, used or revoked invitations are rejected",
                "The account is created inside the inviting lab with the invited role"],
         states=["expired", "validationError", "success"], sensitive={"auditActions": ["InviteAccepted"], "secrets": ["token", "password"]},
         ai=NO_AI, tests=("happy", "expired", "reused"), related=["ADM-06", "PLT-03"]),
    spec("AUTH-06", "auth", "/setup", "AuthPage", None, "Auth.Setup.Title", access="anonymous",
         entities=["ApplicationUser"],
         fields=[f("token", "text", True), f("fullName", "text", True, maxLength=150), f("email", "email", True), f("password", "password", True)],
         rules=["Open only while no platform owner exists", "Requires the setup token from configuration or the startup log",
                "No default credentials exist in code or seed data"],
         states=["locked", "validationError"], sensitive={"secrets": ["token", "password"]}, ai=NO_AI,
         tests=("happy", "closedAfterFirstOwner", "wrongToken")),
    spec("AUTH-07", "auth", "/account/password", "FormPage", None, "Nav.ChangePassword",
         entities=["ApplicationUser"],
         fields=[f("currentPassword", "password", True), f("newPassword", "password", True), f("confirmPassword", "password", True)],
         rules=["Password policy: 10+ chars with upper, lower and digit", "Changing the password rotates the security stamp (other sessions end)"],
         states=["validationError", "success"], sensitive={"secrets": ["currentPassword", "newPassword"]}, ai=NO_AI,
         tests=("happy", "wrongCurrent")),
    spec("AUTH-09", "auth", "/session-expired", "StatusPage", None, "Session.Title", access="anonymous",
         rules=["Shown after a session is ended because the user was deactivated or the lab suspended"],
         states=["expired"], offline={"mode": "available"}, ai=NO_AI, tests=("renders",)),
    spec("SYS-01", "system", "/account", "DetailsTabs", None, "Account.Title", entities=["ApplicationUser", "UserProfile"],
         rules=["Shows the effective permissions resolved on the server"], ai=EXPLAIN("user.roles", "user.permissions"), tests=("renders",)),
    spec("SYS-02", "system", "/account/preferences", "FormPage", None, "Nav.Preferences", entities=["ApplicationUser"],
         fields=[f("culture", "enum", True, source="ar-EG|en-US"), f("theme", "enum", True, source="light|dark|system")],
         rules=["Language switches layout direction (RTL for Arabic)", "Preferences are stored on the account and in a cookie"],
         tests=("happy",)),
    spec("SYS-05", "system", "/search", "ListPage", None, "Search.Title", columns=["type", "number", "title"],
         rules=["Global search returns only records the user may view (planned with later modules)"], tests=("renders",)),
    spec("SYS-06", "system", "/offline", "StatusPage", None, "Offline.Title", access="anonymous",
         rules=["Served by the service worker when offline; no lab data is cached on the device"],
         offline={"mode": "available", "why": "offline fallback page"}, ai=NO_AI, tests=("renders",)),
    spec("SYS-07", "system", "/error/{code}", "StatusPage", None, "Error.500.Title", access="anonymous",
         rules=["403/404/409/500/503 pages never reveal whether a record of another lab exists"],
         states=["forbidden", "notFound", "conflict"], ai=NO_AI, tests=("renders",)),
    spec("NTF-01", "notifications", "/notifications", "ListPage", None, "Nav.Notifications", columns=["when", "title", "status"],
         states=["empty"], rules=["Only notifications addressed to the current user"], tests=("renders",)),

    # ---- Platform portal ----
    spec("PLT-01", "platform", "/platform", "DashboardPage", "platform.dashboard.read", "Platform.Dashboard.Title",
         entities=["Tenant", "TenantSubscription", "SubscriptionPlan"], api=[("GET", "/api/v1/platform/dashboard", "KPIs")],
         rules=["Platform staff see subscription data only, never a lab's operational data"],
         ai=EXPLAIN("tenants.count", "subscriptions.byPlan"), tests=("happy", "permissionDenied"), related=["PLT-02"]),
    spec("PLT-02", "platform", "/platform/tenants", "ListPage", "platform.tenants.read", "Nav.Tenants",
         entities=["Tenant"], api=[("GET", "/api/v1/platform/tenants", "Paged list")],
         columns=["name", "slug", "plan", "status", "users", "subscriptionEnds", "createdAt"], filters=["search", "status"],
         rules=["Server-side paging (max 100 per page)"], states=["loading", "empty"], tests=("happy", "permissionDenied"),
         related=["PLT-03", "PLT-04"]),
    spec("PLT-03", "platform", "/platform/tenants/new", "FormPage", "platform.tenants.create", "Platform.Tenants.New",
         entities=["Tenant", "TenantSubscription", "Branch", "AppRole", "RolePermission", "DocumentSequence", "UserInvitation"],
         api=[("POST", "/api/v1/platform/tenants", "Provision a lab")],
         fields=[f("name", "text", True, maxLength=200), f("slug", "slug", True, pattern="^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$"),
                 f("baseCurrencyCode", "code", True, maxLength=3), f("defaultCulture", "enum", True, source="ar-EG|en-US"),
                 f("contactPhone", "text", maxLength=32), f("mainBranchName", "text", maxLength=150),
                 f("planId", "lookup", True, source="plans"), f("trialDays", "integer", min=0, max=90),
                 f("ownerFullName", "text", True, maxLength=150), f("ownerEmail", "email", True)],
         rules=["Creates the lab, main branch, 13 system roles, numbering sequences and an owner invitation in one transaction",
                "Slug and owner e-mail must be unique across the platform",
                "The invitation link is shown once; only its hash is stored (valid 7 days)"],
         states=["validationError", "success"], sensitive={"auditActions": ["Create", "Invite"], "secrets": ["inviteToken"]},
         ai=NO_AI, tests=("happy", "duplicateSlug", "permissionDenied"), related=["PLT-02", "AUTH-05"]),
    spec("PLT-04", "platform", "/platform/tenants/{id}", "DetailsTabs", "platform.tenants.read", "Platform.Tenant.Profile",
         actions={"suspend": "platform.tenants.manage", "activate": "platform.tenants.manage"},
         entities=["Tenant", "TenantSubscription"],
         api=[("GET", "/api/v1/platform/tenants/{id}", "Details"), ("POST", "/api/v1/platform/tenants/{id}/status", "Suspend / reactivate")],
         fields=[f("reason", "textarea", True, maxLength=500)],
         rules=["Suspending requires a reason and ends all lab sessions on the next request", "No data is deleted on suspension"],
         states=["notFound", "validationError"], sensitive={"auditActions": ["StatusChange"]},
         ai=EXPLAIN("tenant.status", "tenant.plan"), tests=("happy", "suspendBlocksLogin", "reasonRequired"),
         notes="Covers PLT-04 (details) and PLT-05 (suspend/reactivate dialog)."),
    spec("PLT-06", "platform", "/platform/plans", "ListPage", "platform.tenants.read", "Nav.Plans",
         entities=["SubscriptionPlan"], api=[("GET", "/api/v1/platform/plans", "Plans")],
         columns=["name", "price", "users", "branches", "workOrders", "storage", "features", "tenants"],
         rules=["Prices are placeholders until commercial launch"], tests=("renders",)),
    spec("PLT-12", "platform", "/platform/audit", "ListPage", "platform.audit.read", "Nav.PlatformAudit",
         entities=["AuditLog"], columns=["when", "who", "action", "entity", "details", "ip"], filters=["action", "from", "to"],
         rules=["Shows only platform-level entries (TenantId is null)", "Audit entries are append-only"],
         sensitive={"secrets": ["masked fields"]}, tests=("happy", "permissionDenied")),

    # ---- Lab administration ----
    spec("ADM-01", "admin", "/admin", "DashboardPage", "admin.dashboard.read", "Admin.Dashboard.Title",
         entities=["Branch", "Department", "UserProfile", "AppRole", "AuditLog", "TenantSubscription"],
         rules=["Setup checklist is computed from real data", "Cards are limited by the user's permissions"],
         ai=EXPLAIN("setup.steps", "subscription.status"), tests=("happy", "crossTenant")),
    spec("ADM-03", "admin", "/admin/branches", "ListPage", "admin.branches.manage", "Nav.Branches",
         routes=["/admin/branches", "/admin/branches/{id}"], entities=["Branch"],
         api=[("GET", "/api/v1/branches", "List"), ("GET", "/api/v1/branches/{id}", "Details"),
              ("POST", "/api/v1/branches", "Create"), ("PUT", "/api/v1/branches/{id}", "Update")],
         columns=["code", "name", "phone", "isMain", "status"], filters=["search"],
         fields=[f("code", "code", True, maxLength=16), f("name", "text", True, maxLength=150), f("address", "text", maxLength=300),
                 f("phone", "text", maxLength=32), f("isActive", "boolean"), f("rowVersion", "hidden")],
         rules=["Code unique per lab", "Plan branch limit enforced on create", "The main branch must stay active",
                "RowVersion detects concurrent edits"],
         states=["loading", "empty", "validationError", "conflict"], sensitive={"auditActions": ["Create", "Update"]},
         tests=TENANT_TESTS + ("planLimit", "concurrency")),
    spec("ADM-04", "admin", "/admin/departments", "ListPage", "admin.departments.manage", "Nav.Departments",
         routes=["/admin/departments", "/admin/departments/{id}"], entities=["Department", "Branch"],
         columns=["code", "name", "branch", "type", "status"], filters=["search", "branch"],
         fields=[f("code", "code", True, maxLength=16), f("name", "text", True, maxLength=150),
                 f("branchId", "lookup", source="branches"), f("isProduction", "boolean"), f("isActive", "boolean"), f("rowVersion", "hidden")],
         rules=["Code unique per lab", "Production departments become work-order stages (phase 3)"],
         states=["loading", "empty", "validationError", "conflict"], sensitive={"auditActions": ["Create", "Update"]},
         tests=TENANT_TESTS + ("concurrency",)),
    spec("ADM-05", "admin", "/admin/users", "ListPage", "admin.users.read", "Nav.Users",
         actions={"revokeInvitation": "admin.users.manage"}, entities=["UserProfile", "UserInvitation", "UserRole"],
         columns=["name", "email", "roles", "lastLogin", "status"], filters=["search"],
         rules=["Pending invitations listed separately and can be revoked"], states=["loading", "empty"],
         sensitive={"auditActions": ["StatusChange"]}, tests=TENANT_TESTS),
    spec("ADM-06", "admin", "/admin/users/{id}", "DetailsTabs", "admin.users.read", "Admin.Users.Invite",
         routes=["/admin/users/invite", "/admin/users/{id}"],
         actions={"invite": "admin.users.manage", "assignRoles": "admin.users.manage", "deactivate": "admin.users.manage"},
         entities=["UserInvitation", "UserProfile", "UserRole", "AppRole"],
         fields=[f("fullName", "text", True, maxLength=150), f("email", "email", True), f("roleId", "lookup", True, source="roles"),
                 f("roleIds", "multiLookup", source="roles")],
         rules=["Plan user limit includes pending invitations", "One e-mail = one account across the platform",
                "You cannot deactivate yourself or the last active owner", "Role changes apply on the next request"],
         states=["validationError", "notFound"], sensitive={"auditActions": ["Invite", "RoleAssignment", "StatusChange"], "secrets": ["inviteToken"]},
         tests=TENANT_TESTS + ("planLimit", "lastOwner")),
    spec("ADM-08", "admin", "/admin/roles", "ListPage", "admin.roles.manage", "Nav.Roles",
         entities=["AppRole"], columns=["name", "type", "users", "permissions"],
         fields=[f("name", "text", True, maxLength=100), f("description", "text", maxLength=300), f("copyFromRoleId", "lookup", source="roles")],
         rules=["System roles cannot be deleted", "Custom roles can copy permissions from an existing role"],
         sensitive={"auditActions": ["Create"]}, tests=TENANT_TESTS),
    spec("ADM-09", "admin", "/admin/roles/{id}/permissions", "Custom", "admin.roles.manage", "Admin.Roles.Matrix",
         entities=["AppRole", "RolePermission", "Permission"],
         fields=[f("keys", "multiLookup", source="permissions"), f("rowVersion", "hidden")],
         rules=["The owner role always has every permission and is read-only", "Changes take effect on the next request",
                "Every change is audited with added/removed keys"],
         states=["conflict", "locked"], sensitive={"auditActions": ["PermissionChange"]},
         ai=EXPLAIN("role.name", "permissions.granted"), tests=TENANT_TESTS + ("ownerLocked", "concurrency"),
         notes="Permission matrix; grouped by module with select-all per module."),
    spec("ADM-11", "admin", "/admin/numbering", "Custom", "admin.numbering.manage", "Nav.Numbering",
         entities=["DocumentSequence"],
         fields=[f("prefix", "code", True, maxLength=10), f("padding", "integer", True, min=3, max=10), f("resetYearly", "boolean")],
         rules=["Numbers are allocated under a row lock: gap-free and unique per lab, branch, type and year",
                "Changing the format never renumbers issued documents"],
         sensitive={"auditActions": ["Update"]}, tests=TENANT_TESTS + ("concurrentAllocation",)),
    spec("ADM-25", "admin", "/admin/audit", "ListPage", "admin.audit.read", "Nav.Audit",
         entities=["AuditLog"], columns=["when", "who", "action", "entity", "details", "ip"], filters=["action", "from", "to"],
         rules=["Only the current lab's entries", "Sensitive fields are masked", "Append-only"],
         tests=TENANT_TESTS),
    spec("ADM-26", "admin", "/admin/subscription", "DashboardPage", "admin.subscription.read", "Nav.Subscription",
         entities=["TenantSubscription", "SubscriptionPlan"],
         rules=["Read-only for the lab; changes are made by the platform team"], tests=("happy", "permissionDenied")),
]


def main():
    if "--bootstrap" in sys.argv:
        for s in SPECS:
            (OUT / f"{s['id']}.screen.json").write_text(json.dumps(s, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    specs = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(OUT.glob("*.screen.json"))]

    rows = ["| ID | الوحدة | المسار | النمط | صلاحية العرض | الحالة |", "|---|---|---|---|---|---|"]
    for s in specs:
        view = s["permissions"]["view"] or s["permissions"]["access"]
        rows.append(f"| [{s['id']}](../screens/{s['id']}.screen.json) | {s['module']} | `{s['route']}` | {s['pattern']} | `{view}` | {s['status']} |")
    (ROOT / "docs" / "ai" / "SCREEN_REGISTRY.md").write_text(
        "# سجل الشاشات (Screen Registry)\n\n"
        "> ملف مولَّد آليًا من `docs/screens/*.screen.json` بواسطة `python3 tools/screens/generate.py`. لا تعدّله يدويًا.\n\n"
        f"عدد الشاشات الموثقة: **{len(specs)}**. اختبارات المعمارية تفشل إذا وُجدت صفحة تحمل `[Screen]` بلا ملف مواصفات، "
        "أو ملف يخالف المخطط، أو صلاحية غير معرّفة في الكتالوج.\n\n" + "\n".join(rows) + "\n",
        encoding="utf-8")
    print(f"indexed {len(specs)} specs")


if __name__ == "__main__":
    main()
