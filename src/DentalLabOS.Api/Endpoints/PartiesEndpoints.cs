using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Security;

namespace DentalLabOS.Api.Endpoints;

/// <summary>Customers, dentists, groups and suppliers (CUS, SUP). Services re-check permissions; tenant filters scope every query.</summary>
public static class PartiesEndpoints
{
    public static RouteGroupBuilder MapPartiesEndpoints(this RouteGroupBuilder api)
    {
        var customers = api.MapGroup("/customers").WithTags("Customers")
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.Read));

        customers.MapGet("/", (int? page, int? pageSize, string? search, string? sort, bool? active, Guid? groupId, string? type,
                CustomerService svc, CancellationToken ct) =>
                svc.ListAsync(new PageQuery(page ?? 1, pageSize ?? 20, search, sort), new CustomerFilter(active, groupId, type), ct))
            .WithSummary("List customers (paged; search matches name, code, phones and dentist names)");

        customers.MapGet("/options", (string? search, CustomerService svc, CancellationToken ct) => svc.OptionsAsync(search, ct))
            .WithSummary("Active customers for pickers (top 50)");

        customers.MapGet("/{id:guid}", (Guid id, CustomerService svc, CancellationToken ct) => svc.GetAsync(id, ct))
            .WithName("GetCustomer");

        customers.MapPost("/", async (CustomerUpsertRequest request, CustomerService svc, CancellationToken ct) =>
            {
                var created = await svc.CreateAsync(request, ct);
                return TypedResults.CreatedAtRoute(created, "GetCustomer", new { id = created.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.Manage))
            .WithSummary("Create a customer; an empty code takes the next number (C-00001)");

        customers.MapPut("/{id:guid}", (Guid id, CustomerUpsertRequest request, CustomerService svc, CancellationToken ct) => svc.UpdateAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.Manage))
            .WithSummary("Update a customer (send the RowVersion you read). Credit terms need customers.credit.manage");

        customers.MapGet("/{id:guid}/dentists", (Guid id, CustomerService svc, CancellationToken ct) => svc.ListDentistsAsync(id, ct));

        customers.MapPost("/{id:guid}/dentists", (Guid id, DentistUpsertRequest request, CustomerService svc, CancellationToken ct) =>
                svc.SaveDentistAsync(id, null, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.DentistsManage));

        customers.MapPut("/{id:guid}/dentists/{dentistId:guid}", (Guid id, Guid dentistId, DentistUpsertRequest request, CustomerService svc, CancellationToken ct) =>
                svc.SaveDentistAsync(id, dentistId, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.DentistsManage));

        customers.MapGet("/{id:guid}/prices", (Guid id, CustomerService svc, CancellationToken ct) => svc.ListPricesAsync(id, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Read))
            .WithSummary("Customer-specific prices (highest pricing priority)");

        customers.MapPost("/{id:guid}/prices", (Guid id, CustomerPriceUpsertRequest request, CustomerService svc, CancellationToken ct) =>
                svc.AddPriceAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Manage))
            .WithSummary("Add a special price; periods for the same item must not overlap");

        customers.MapDelete("/{id:guid}/prices/{priceId:guid}", async (Guid id, Guid priceId, CustomerService svc, CancellationToken ct) =>
            {
                await svc.DeletePriceAsync(id, priceId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Manage));

        var groups = api.MapGroup("/customer-groups").WithTags("Customers")
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.Read));
        groups.MapGet("/", (CustomerService svc, CancellationToken ct) => svc.ListGroupsAsync(ct));
        groups.MapPost("/", (CustomerGroupUpsertRequest request, CustomerService svc, CancellationToken ct) => svc.SaveGroupAsync(null, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.GroupsManage));
        groups.MapPut("/{id:guid}", (Guid id, CustomerGroupUpsertRequest request, CustomerService svc, CancellationToken ct) => svc.SaveGroupAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.GroupsManage));
        groups.MapDelete("/{id:guid}", async (Guid id, CustomerService svc, CancellationToken ct) =>
            {
                await svc.DeleteGroupAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Customers.GroupsManage))
            .WithSummary("Delete an unused group");

        var suppliers = api.MapGroup("/suppliers").WithTags("Suppliers")
            .RequireAuthorization(PermissionPolicy.For(Permissions.Suppliers.Read));

        suppliers.MapGet("/", (int? page, int? pageSize, string? search, string? sort, bool? active, SupplierService svc, CancellationToken ct) =>
            svc.ListAsync(new PageQuery(page ?? 1, pageSize ?? 20, search, sort), active, ct));

        suppliers.MapGet("/options", (string? search, SupplierService svc, CancellationToken ct) => svc.OptionsAsync(search, ct));

        suppliers.MapGet("/{id:guid}", (Guid id, SupplierService svc, CancellationToken ct) => svc.GetAsync(id, ct)).WithName("GetSupplier");

        suppliers.MapPost("/", async (SupplierUpsertRequest request, SupplierService svc, CancellationToken ct) =>
            {
                var created = await svc.CreateAsync(request, ct);
                return TypedResults.CreatedAtRoute(created, "GetSupplier", new { id = created.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Suppliers.Manage))
            .WithSummary("Create a supplier; an empty code takes the next number (S-00001)");

        suppliers.MapPut("/{id:guid}", (Guid id, SupplierUpsertRequest request, SupplierService svc, CancellationToken ct) => svc.UpdateAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Suppliers.Manage));

        suppliers.MapGet("/{id:guid}/items", (Guid id, SupplierService svc, CancellationToken ct) => svc.ListItemsAsync(id, ct))
            .WithSummary("Items this supplier provides (prices hidden without inventory.valuation.read)");

        suppliers.MapPost("/{id:guid}/items", (Guid id, SupplierItemUpsertRequest request, SupplierService svc, CancellationToken ct) =>
                svc.SaveItemAsync(id, null, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Suppliers.ItemsManage));

        suppliers.MapPut("/{id:guid}/items/{lineId:guid}", (Guid id, Guid lineId, SupplierItemUpsertRequest request, SupplierService svc, CancellationToken ct) =>
                svc.SaveItemAsync(id, lineId, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Suppliers.ItemsManage));

        suppliers.MapDelete("/{id:guid}/items/{lineId:guid}", async (Guid id, Guid lineId, SupplierService svc, CancellationToken ct) =>
            {
                await svc.DeleteItemAsync(id, lineId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Suppliers.ItemsManage));

        return api;
    }
}
