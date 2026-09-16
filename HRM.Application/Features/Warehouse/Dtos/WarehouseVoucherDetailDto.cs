using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Dtos;

public sealed class WarehouseVoucherDetailDto
{
    public long VoucherDetailId { get; init; }
    public long VoucherId { get; init; }
    public int LineNo { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? LotNumber { get; init; }
    public decimal QtyKg { get; init; }
    public int? Bags { get; init; }
    public int? SlotId { get; init; }
    public int? PurposeId { get; init; }
    public bool IsIncrease { get; init; }
    public DateTime? MovementDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public VoucherDetailType VoucherType { get; init; }
    public string? Note { get; init; }
}
