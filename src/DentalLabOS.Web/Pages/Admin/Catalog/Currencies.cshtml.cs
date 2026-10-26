using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Catalog;

[Screen("ADM-13")]
[HasPermission(Permissions.Admin.CurrenciesManage)]
public sealed class CurrenciesModel(ReferenceDataService reference, IClock clock) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public RateInput Rate { get; set; } = new();

    public IReadOnlyList<CurrencyDto> Currencies { get; private set; } = [];
    public IReadOnlyList<ExchangeRateDto> Rates { get; private set; } = [];

    public sealed class InputModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DecimalPlaces { get; set; } = 2;
        public string? RowVersion { get; set; }
    }

    public sealed class RateInput
    {
        public string FromCode { get; set; } = string.Empty;
        public string ToCode { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public decimal Rate { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Edit is { } id && Currencies.FirstOrDefault(c => c.Id == id) is { } c)
        {
            Input = new InputModel { Code = c.Code, Name = c.Name, DecimalPlaces = c.DecimalPlaces, RowVersion = c.RowVersion };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await TryExecuteAsync(() => reference.SaveCurrencyAsync(Edit, new CurrencyUpsertRequest(Input.Code, Input.Name, Input.DecimalPlaces, Input.RowVersion))))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/admin/currencies");
    }

    public async Task<IActionResult> OnPostRateAsync()
    {
        if (!await TryExecuteAsync(() => reference.SetRateAsync(new ExchangeRateRequest(Rate.FromCode, Rate.ToCode, Rate.Date, Rate.Rate)), "Rate"))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/admin/currencies#rates");
    }

    private async Task LoadAsync()
    {
        Currencies = await reference.ListCurrenciesAsync();
        Rates = await reference.ListRatesAsync(50);
        if (Rate.Date == default)
        {
            Rate.Date = DateOnly.FromDateTime(clock.UtcNow);
            Rate.ToCode = Currencies.FirstOrDefault(c => c.IsBase)?.Code ?? string.Empty;
            Rate.FromCode = Currencies.FirstOrDefault(c => !c.IsBase)?.Code ?? string.Empty;
        }
    }
}
