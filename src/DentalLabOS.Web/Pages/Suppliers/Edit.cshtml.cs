using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Suppliers;

[Screen("SUP-02")]
[HasPermission(Permissions.Suppliers.Manage)]
public sealed class EditModel(SupplierService suppliers) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";

    [BindProperty] public InputModel Input { get; set; } = new();

    public bool IsNew => !Guid.TryParse(Id, out _);

    public sealed class InputModel
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public int PaymentTermsDays { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal? Rating { get; set; }
        public bool IsOutsourcePartner { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        if (!IsNew)
        {
            var s = await suppliers.GetAsync(Guid.Parse(Id));
            Input = new InputModel
            {
                Code = s.Code, Name = s.Name, Phone = s.Phone, Email = s.Email, Address = s.Address, TaxNumber = s.TaxNumber,
                PaymentTermsDays = s.PaymentTermsDays, OpeningBalance = s.OpeningBalance, Rating = s.Rating, IsOutsourcePartner = s.IsOutsourcePartner,
                IsActive = s.IsActive, RowVersion = s.RowVersion,
            };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        var request = new SupplierUpsertRequest(i.Code, i.Name, i.Phone, i.Email, i.Address, i.TaxNumber, i.PaymentTermsDays, i.OpeningBalance, i.Rating,
            i.IsOutsourcePartner, i.IsActive, i.RowVersion);
        SupplierDto? saved = null;
        if (!await TryExecuteAsync(async () => saved = IsNew ? await suppliers.CreateAsync(request) : await suppliers.UpdateAsync(Guid.Parse(Id), request)))
        {
            return Page();
        }

        Flash(IsNew ? "Suppliers.Created" : "Common.Saved", saved!.Code);
        return Redirect($"/suppliers/{saved.Id}");
    }
}
