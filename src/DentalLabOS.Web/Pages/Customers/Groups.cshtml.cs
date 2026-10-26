using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Customers;

[Screen("CUS-05")]
[HasPermission(Permissions.Customers.GroupsManage)]
public sealed class GroupsModel(CustomerService customers, PriceListService priceLists) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<CustomerGroupDto> Groups { get; private set; } = [];
    public IReadOnlyList<PriceListDto> PriceLists { get; private set; } = [];

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public Guid? DefaultPriceListId { get; set; }
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Edit is { } id && Groups.FirstOrDefault(g => g.Id == id) is { } g)
        {
            Input = new InputModel { Name = g.Name, DefaultPriceListId = g.DefaultPriceListId, RowVersion = g.RowVersion };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await TryExecuteAsync(() => customers.SaveGroupAsync(Edit, new CustomerGroupUpsertRequest(Input.Name, Input.DefaultPriceListId, Input.RowVersion))))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/customers/groups");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid groupId)
    {
        if (!await TryExecuteAsync(() => customers.DeleteGroupAsync(groupId)))
        {
            FlashError("Validation.InUse");
        }
        else
        {
            Flash("Common.Deleted");
        }

        return Redirect("/customers/groups");
    }

    private async Task LoadAsync()
    {
        Groups = await customers.ListGroupsAsync();
        if (Can(Permissions.PriceLists.Read))
        {
            PriceLists = (await priceLists.ListAsync()).Where(p => p.IsActive).ToList();
        }
    }
}
