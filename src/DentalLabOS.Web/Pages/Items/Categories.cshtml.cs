using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Items;

[Screen("ITM-04")]
[HasPermission(Permissions.Items.CategoriesManage)]
public sealed class CategoriesModel(ItemService items) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<ItemCategoryDto> Categories { get; private set; } = [];

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public string? DefaultType { get; set; }
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        Categories = await items.ListCategoriesAsync();
        if (Edit is { } id && Categories.FirstOrDefault(c => c.Id == id) is { } c)
        {
            Input = new InputModel { Name = c.Name, ParentId = c.ParentId, DefaultType = c.DefaultType, RowVersion = c.RowVersion };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new ItemCategoryUpsertRequest(Input.Name, Input.ParentId, Input.DefaultType, Input.RowVersion);
        if (!await TryExecuteAsync(() => items.SaveCategoryAsync(Edit, request)))
        {
            Categories = await items.ListCategoriesAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/items/categories");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid categoryId)
    {
        if (await TryExecuteAsync(() => items.DeleteCategoryAsync(categoryId)))
        {
            Flash("Common.Deleted");
        }
        else
        {
            FlashError("Validation.InUse");
        }

        return Redirect("/items/categories");
    }
}
