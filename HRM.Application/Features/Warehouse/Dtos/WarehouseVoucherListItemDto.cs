using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Dtos;

public sealed class WarehouseVoucherListItemDto
{
    public long VoucherId { get; init; }
    public string VoucherCode { get; init; } = string.Empty;
    public int VoucherType { get; init; }
    public WarehouseVoucherBusinessTypeDto BusinessType { get; init; } = new("Unknown", "Chưa xác định", "Unclassified");
    public string? PurchaseOrderCode { get; init; }
    public string? Status { get; init; }
    public DateTime CreatedDate { get; init; }
    public int? RequestId { get; init; }
    public string RequestCode { get; init; } = string.Empty;
    public string RequestName { get; init; } = string.Empty;
    public WarehouseRequestStatus? ReqStatus { get; init; }
    public WareHouseRequestType? ReqType { get; init; }
    public string CodeFromRequest { get; init; } = string.Empty;
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public Guid CreatedBy { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
    public bool CanViewSupplier { get; init; }
    public string? SupplierName { get; init; }
    public string? SupplierExternalId { get; init; }
    public string? Comments { get; init; }
    public IReadOnlyList<WarehouseVoucherDetailDto> Details { get; init; } = [];
}
