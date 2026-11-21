namespace HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;

public sealed record ProductionTrackingDto(Guid Id, string Code, string? ProductCode, string? ProductName,
    string Status, decimal RequestedQuantity, decimal? ProductionQuantity, DateTime RequiredDate,
    DateTime? ExpectedDate, DateTime? ManufacturingDate);
