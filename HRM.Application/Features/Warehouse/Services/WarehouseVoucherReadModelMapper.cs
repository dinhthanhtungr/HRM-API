using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Features.Warehouse.Services;

public static class WarehouseVoucherReadModelMapper
{
    public static WarehouseVoucherDetailDto ToDetailDto(WarehouseVoucherDetail detail, DateTime? movementDate) => new()
    {
        VoucherDetailId = detail.VoucherDetailId,
        VoucherId = detail.VoucherId,
        LineNo = detail.LineNo,
        ProductCode = detail.ProductCode,
        ProductName = detail.ProductName,
        LotNumber = detail.LotNumber,
        QtyKg = detail.QtyKg,
        Bags = detail.Bags,
        SlotId = detail.SlotId,
        PurposeId = detail.PurposeId,
        IsIncrease = detail.IsIncrease,
        MovementDate = movementDate,
        ExpiryDate = detail.ExpiryDate,
        VoucherType = detail.VoucherType,
        Note = detail.Note
    };
}
