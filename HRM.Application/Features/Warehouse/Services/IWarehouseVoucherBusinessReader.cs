using HRM.Application.Features.Warehouse.Dtos;

namespace HRM.Application.Features.Warehouse.Services;

public sealed record WarehouseVoucherBusinessInput(long VoucherId, int VoucherType, string? SourceCode);
public sealed record WarehouseVoucherBusinessInfo(
    WarehouseVoucherBusinessTypeDto BusinessType,
    string? PurchaseOrderCode = null,
    string? SupplierName = null,
    string? SupplierExternalId = null,
    string? Comments = null,
    bool CanViewSupplier = false);

public interface IWarehouseVoucherBusinessReader
{
    Task<IReadOnlyDictionary<long, WarehouseVoucherBusinessInfo>> ReadAsync(
        Guid companyId, IReadOnlyCollection<WarehouseVoucherBusinessInput> inputs, CancellationToken ct);
}
