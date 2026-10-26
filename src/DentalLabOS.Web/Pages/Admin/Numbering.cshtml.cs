using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin;

[Screen("ADM-11")]
[HasPermission(Permissions.Admin.NumberingManage)]
public sealed class NumberingModel(NumberingAdminService numbering) : AppPageModel
{
    public IReadOnlyList<DocumentSequenceDto> Sequences { get; private set; } = [];

    public async Task OnGetAsync() => Sequences = await numbering.ListAsync();

    public async Task<IActionResult> OnPostAsync(Guid sequenceId, string prefix, int padding, bool resetYearly, string rowVersion)
    {
        if (await TryExecuteAsync(() => numbering.UpdateAsync(sequenceId, new UpdateSequenceRequest(prefix, padding, resetYearly, rowVersion)), prefix: string.Empty))
        {
            Flash("Common.Saved");
            return RedirectToPage();
        }

        // Field errors of a single row are shown in the summary.
        foreach (var key in ModelState.Keys.Where(k => k.Length > 0).ToList())
        {
            foreach (var e in ModelState[key]!.Errors)
            {
                ModelState.AddModelError(string.Empty, e.ErrorMessage);
            }
        }

        Sequences = await numbering.ListAsync();
        return Page();
    }
}
