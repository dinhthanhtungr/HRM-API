namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

public sealed record ManufacturingDeliveryPromisePatch(Guid OrderId, Guid DetailId, string Revision,
    IReadOnlyList<ManufacturingDeliveryPromiseDate> Dates);

public sealed record ManufacturingDeliveryPromiseDate(Guid ScheduleId, DateOnly? PromisedDate);
