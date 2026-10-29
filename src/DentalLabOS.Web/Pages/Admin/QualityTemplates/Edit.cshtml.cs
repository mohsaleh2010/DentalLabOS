using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.QualityTemplates;

/// <summary>ADM-17 checklist editor. Items are replaced as a whole; past inspections keep the texts they were checked against.</summary>
[Screen("ADM-17")]
[HasPermission(Permissions.Admin.QualityManage)]
public sealed class EditModel(QualityService quality) : AppPageModel
{
    private const int BlankRows = 3;

    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";
    [BindProperty] public InputModel Input { get; set; } = new();

    public bool IsNew => !Guid.TryParse(Id, out _);
    public QualityTemplateDto? Current { get; private set; }

    public sealed class ItemInput
    {
        public Guid? Id { get; set; }
        public string? Text { get; set; }
        public bool IsCritical { get; set; }
    }

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; }
        public List<ItemInput> Items { get; set; } = [];
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        if (!IsNew)
        {
            Current = await quality.GetTemplateAsync(Guid.Parse(Id));
            Input = new InputModel
            {
                Name = Current.Name, Description = Current.Description, IsActive = Current.IsActive, IsDefault = Current.IsDefault, RowVersion = Current.RowVersion,
                Items = Current.Items.Select(i => new ItemInput { Id = i.Id, Text = i.Text, IsCritical = i.IsCritical }).ToList(),
            };
        }

        Pad();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        var items = i.Items.Where(x => !string.IsNullOrWhiteSpace(x.Text)).Select(x => new QualityTemplateItemRequest(x.Text!, x.IsCritical, x.Id)).ToList();
        QualityTemplateDto? saved = null;
        if (!await TryExecuteAsync(async () => saved = await quality.SaveTemplateAsync(IsNew ? null : Guid.Parse(Id),
                new QualityTemplateUpsertRequest(i.Name, items, i.Description, i.IsActive, i.IsDefault, i.RowVersion))))
        {
            if (!IsNew)
            {
                Current = await quality.GetTemplateAsync(Guid.Parse(Id));
            }

            Pad();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/admin/quality-templates/{saved!.Id}");
    }

    private void Pad()
    {
        for (var k = 0; k < BlankRows; k++)
        {
            Input.Items.Add(new ItemInput());
        }
    }
}
