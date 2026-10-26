using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Customers;

[Screen("CUS-02")]
[HasPermission(Permissions.Customers.Manage)]
public sealed class EditModel(CustomerService customers, PriceListService priceLists) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";

    [BindProperty] public InputModel Input { get; set; } = new();

    public bool IsNew => !Guid.TryParse(Id, out _);
    public IReadOnlyList<CustomerGroupDto> Groups { get; private set; } = [];
    public IReadOnlyList<PriceListDto> PriceLists { get; private set; } = [];

    public sealed class InputModel
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Clinic";
        public Guid? GroupId { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public int PaymentTermsDays { get; set; }
        public decimal CreditLimit { get; set; }
        public bool BlockWhenOverCredit { get; set; }
        public Guid? PriceListId { get; set; }
        public decimal OpeningBalance { get; set; }
        public string? InternalNotes { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsNew)
        {
            var c = await customers.GetAsync(Guid.Parse(Id));
            Input = new InputModel
            {
                Code = c.Code, Name = c.Name, Type = c.Type, GroupId = c.GroupId, Phone = c.Phone, Mobile = c.Mobile, Email = c.Email,
                City = c.City, Address = c.Address, TaxNumber = c.TaxNumber, PaymentTermsDays = c.PaymentTermsDays, CreditLimit = c.CreditLimit,
                BlockWhenOverCredit = c.BlockWhenOverCredit, PriceListId = c.PriceListId, OpeningBalance = c.OpeningBalance,
                InternalNotes = c.InternalNotes, IsActive = c.IsActive, RowVersion = c.RowVersion,
            };
        }

        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        var request = new CustomerUpsertRequest(i.Code, i.Name, i.Type, i.GroupId, i.Phone, i.Mobile, i.Email, i.Address, i.City, i.TaxNumber,
            i.PaymentTermsDays, i.CreditLimit, i.BlockWhenOverCredit, i.PriceListId, i.OpeningBalance, i.InternalNotes, i.IsActive, i.RowVersion);
        CustomerDto? saved = null;
        var ok = await TryExecuteAsync(async () => saved = IsNew ? await customers.CreateAsync(request) : await customers.UpdateAsync(Guid.Parse(Id), request));
        if (!ok)
        {
            await LoadOptionsAsync();
            return Page();
        }

        Flash(IsNew ? "Customers.Created" : "Common.Saved", saved!.Code);
        return Redirect($"/customers/{saved.Id}");
    }

    private async Task LoadOptionsAsync()
    {
        Groups = await customers.ListGroupsAsync();
        if (Can(Permissions.PriceLists.Read))
        {
            PriceLists = (await priceLists.ListAsync()).Where(p => p.IsActive).ToList();
        }
    }
}
