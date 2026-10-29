using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Admin.QualityTemplates;

[Screen("ADM-17")]
[HasPermission(Permissions.Admin.QualityManage)]
public sealed class IndexModel(QualityService quality) : AppPageModel
{
    public IReadOnlyList<QualityTemplateListItemDto> Items { get; private set; } = [];

    public async Task OnGetAsync() => Items = await quality.ListTemplatesAsync();
}
