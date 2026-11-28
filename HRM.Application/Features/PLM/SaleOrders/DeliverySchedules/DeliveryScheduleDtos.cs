using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

public sealed record DeliveryScheduleFeatureDto(bool Enabled, bool CanEditRequest, bool CanEditPromise);
public sealed record DeliverySchedulePreviewRequest(decimal Quantity, Guid? ExcludingDeliveryOrderId = null);
public sealed record DeliverySchedulePreviewDto(bool Enabled, IReadOnlyList<DeliveryScheduleRowDto> Schedules,
    IReadOnlyList<DeliveryScheduleAllocationPart> Allocations, decimal UnscheduledQuantity);

public sealed record DeliveryScheduleInput(
    [property: JsonRequired] Guid? Id,
    [property: JsonRequired] decimal Quantity,
    [property: JsonRequired] DateOnly RequestedDate,
    [property: JsonRequired] DateOnly? PromisedDate,
    [property: JsonRequired] string? RequestNote,
    [property: JsonRequired] string? PromiseNote);
public sealed record SaveDeliverySchedulesRequest(
    [property: JsonRequired] string Revision,
    [property: JsonRequired] IReadOnlyList<DeliveryScheduleInput> Items,
    [property: JsonRequired] IReadOnlyList<Guid> RemoveIds);
public sealed record DeliveryScheduleRowDto(Guid Id, decimal Quantity, decimal AllocatedQuantity,
    DateOnly RequestedDate, DateOnly? PromisedDate, string? RequestNote, string? PromiseNote);
public sealed record DeliveryScheduleWorkspaceDto(bool Enabled, bool CanEditRequest, bool CanEditPromise,
    string Revision, decimal OrderedQuantity, DateOnly? LegacyRequestedDate, DateOnly? LegacyPromisedDate,
    IReadOnlyList<DeliveryScheduleRowDto> Items, bool HasUnreconciledDelivery = false)
{
    public DeliveryDateSummaryDto DeliveryDates => DeliveryDateSummaryDto.From(this);
}

public sealed record DeliveryPromiseDateInput(
    [property: JsonRequired] Guid ScheduleId,
    [property: JsonRequired] DateOnly? PromisedDate);

public sealed record UpdateDeliveryPromisesRequest(
    [property: JsonRequired] string Revision,
    [property: JsonRequired] IReadOnlyList<DeliveryPromiseDateInput> Items);
