using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.Warehouse.Dtos;

public sealed record StockJourneyDto(
    string ProductCode, string? LotNumber, string SelectedMovementKey,
    bool CanTraceLot, bool IsScopeLimited, DateTime? FirstRecordedAt, DateTime? LastRecordedAt,
    PagedResult<StockJourneyEventDto> Events,
    IReadOnlyList<StockJourneyLocationDto> CurrentLocations,
    IReadOnlyList<StockJourneyUsageDto> ProductionAllocations)
{
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record StockJourneyEventDto(
    StockMovementDto Movement, IReadOnlyList<StockMovementLineDto> Lines)
{
    public string DestinationStatus { get; init; } = "NotApplicable";
    public WarehouseVoucherBusinessTypeDto? VoucherBusinessType { get; init; }
    public bool ShowMissingDestinationWarning => DestinationStatus == "Unresolved";
}

public sealed record StockJourneyLocationDto(
    string ShelfCode, int StockType, decimal QtyKg, bool IsActive);

public sealed record StockJourneyUsageDto(
    long BufferId, string? VaCode, string? MfgCode, string? OperationName,
    decimal IssuedKg, decimal UsedKg, decimal ReturnedKg, decimal? RemainingKg);
