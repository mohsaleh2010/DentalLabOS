using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.My;

[Screen("ORD-12")]
[HasPermission(Permissions.Production.OwnTasks)]
public sealed class TasksModel(ProductionService production) : AppPageModel
{
    public MyTasksDto Tasks { get; private set; } = default!;

    public async Task OnGetAsync() => Tasks = await production.MyTasksAsync();
}
