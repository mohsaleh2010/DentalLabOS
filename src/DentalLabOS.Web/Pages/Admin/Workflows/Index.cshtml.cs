using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Admin.Workflows;

[Screen("ADM-15")]
[HasPermission(Permissions.Admin.WorkflowsManage)]
public sealed class IndexModel(WorkflowService workflows) : AppPageModel
{
    public IReadOnlyList<WorkflowListItemDto> Items { get; private set; } = [];

    public async Task OnGetAsync() => Items = await workflows.ListAsync();
}
