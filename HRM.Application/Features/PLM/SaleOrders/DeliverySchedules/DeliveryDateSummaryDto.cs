using System.Text.Json.Serialization;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

public sealed record DeliveryDateItemDto(Guid? ScheduleId, decimal Quantity,
    DateOnly? RequestedDate, DateOnly? PromisedDate, bool IsStored);

/// <summary>Ngày yêu cầu và ngày hứa được ghép theo đúng đợt, không suy ngày hứa từ ngày yêu cầu.</summary>
public sealed record DeliveryDateSummaryDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DeliveryDateMode>))] DeliveryDateMode Mode,
    [property: JsonConverter(typeof(JsonStringEnumConverter<DeliveryDateSource>))] DeliveryDateSource Source,
    bool CanUpdatePromises, IReadOnlyList<DeliveryDateItemDto> Items)
{
    internal static DeliveryDateSummaryDto From(DeliveryScheduleWorkspaceDto workspace)
    {
        if (!workspace.Enabled) return new(DeliveryDateMode.Unavailable, DeliveryDateSource.Unavailable, false, []);
        if (workspace.Items.Count == 0)
            return new(DeliveryDateMode.Single, DeliveryDateSource.Legacy, false,
                [new(null, workspace.OrderedQuantity, workspace.LegacyRequestedDate, workspace.LegacyPromisedDate, false)]);
        return new(workspace.Items.Count == 1 ? DeliveryDateMode.Single : DeliveryDateMode.Split,
            DeliveryDateSource.Schedules, workspace.CanEditPromise,
            workspace.Items.OrderBy(x => x.RequestedDate).ThenBy(x => x.Id)
                .Select(x => new DeliveryDateItemDto(x.Id, x.Quantity, x.RequestedDate, x.PromisedDate, true)).ToArray());
    }
}
