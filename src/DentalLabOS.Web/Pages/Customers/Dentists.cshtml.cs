using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Customers;

[Screen("CUS-04")]
[HasPermission(Permissions.Customers.DentistsManage)]
public sealed class DentistsModel(CustomerService customers) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public CustomerDto Customer { get; private set; } = default!;
    public IReadOnlyList<DentistDto> Dentists { get; private set; } = [];

    public sealed class InputModel
    {
        public string FullName { get; set; } = string.Empty;
        public string? Specialty { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Edit is { } editId && Dentists.FirstOrDefault(d => d.Id == editId) is { } d)
        {
            Input = new InputModel { FullName = d.FullName, Specialty = d.Specialty, Phone = d.Phone, Email = d.Email, IsActive = d.IsActive, RowVersion = d.RowVersion };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new DentistUpsertRequest(Input.FullName, Input.Specialty, Input.Phone, Input.Email, Edit is null || Input.IsActive, Input.RowVersion);
        if (!await TryExecuteAsync(() => customers.SaveDentistAsync(Id, Edit, request)))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/customers/{Id}/dentists");
    }

    private async Task LoadAsync()
    {
        Customer = await customers.GetAsync(Id);
        Dentists = await customers.ListDentistsAsync(Id);
    }
}
