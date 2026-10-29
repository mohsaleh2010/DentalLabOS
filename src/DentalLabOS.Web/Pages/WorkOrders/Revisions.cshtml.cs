using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.WorkOrders;

[Screen("ORD-08")]
[HasPermission(Permissions.WorkOrders.Read)]
public sealed class RevisionsModel(WorkOrderService workOrders) : AppPageModel
{
    public WorkOrderDto Wo { get; private set; } = default!;
    public IReadOnlyList<RevisionDto> Revisions { get; private set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        Wo = await workOrders.GetAsync(id);
        Revisions = await workOrders.RevisionsAsync(id);
    }

    /// <summary>The stored snapshot is a JSON copy of the prescription before the change.</summary>
    public static IReadOnlyList<(string Key, string Value)> Fields(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.EnumerateObject().Where(p => p.Name != "dentistId")
                .Select(p => (p.Name, p.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Array => string.Join(", ", p.Value.EnumerateArray().Select(e => e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty("toothCode", out var c) ? c.ToString() : e.ToString())),
                    System.Text.Json.JsonValueKind.Null => "—",
                    _ => p.Value.ToString(),
                }))
                .ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
