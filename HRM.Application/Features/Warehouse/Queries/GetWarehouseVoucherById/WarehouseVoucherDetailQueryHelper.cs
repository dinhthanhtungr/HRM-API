using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseVoucherById;

internal sealed class WarehouseVoucherDetailHeaderReadModel
{
    public long VoucherId { get; init; }
    public string VoucherCode { get; init; } = string.Empty;
    public int VoucherType { get; init; }
    public string? Status { get; init; }
    public DateTime CreatedDate { get; init; }
    public int? RequestId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid CreatedBy { get; init; }
    public string RequestCode { get; init; } = string.Empty;
    public string RequestName { get; init; } = string.Empty;
    public WarehouseRequestStatus? ReqStatus { get; init; }
    public WareHouseRequestType? ReqType { get; init; }
    public string CodeFromRequest { get; init; } = string.Empty;
}

internal static class WarehouseVoucherDetailQueryHelper
{
    public static IQueryable<WarehouseVoucherDetailHeaderReadModel> BuildHeaders(
        IQueryable<WarehouseVoucher> vouchers,
        IQueryable<WarehouseRequest> requests) =>
        from voucher in vouchers
        join request in requests on voucher.RequestId equals (int?)request.RequestId into requestGroup
        from request in requestGroup.DefaultIfEmpty()
        select new WarehouseVoucherDetailHeaderReadModel
        {
            VoucherId = voucher.VoucherId,
            VoucherCode = voucher.VoucherCode,
            VoucherType = voucher.VoucherType,
            Status = voucher.Status,
            CreatedDate = voucher.CreatedDate,
            RequestId = voucher.RequestId,
            CompanyId = voucher.CompanyId,
            CreatedBy = voucher.CreatedBy,
            RequestCode = request == null ? string.Empty : request.RequestCode,
            RequestName = request == null ? string.Empty : request.RequestName,
            ReqStatus = request == null ? null : request.ReqStatus,
            ReqType = request == null ? null : request.ReqType,
            CodeFromRequest = request == null ? string.Empty : request.codeFromRequest
        };
}
