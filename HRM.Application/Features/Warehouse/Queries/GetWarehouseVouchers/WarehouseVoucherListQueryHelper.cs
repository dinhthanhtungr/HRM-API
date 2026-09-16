using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseVouchers;

internal sealed class WarehouseVoucherHeaderReadModel
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

internal static class WarehouseVoucherListQueryHelper
{
    public static IQueryable<WarehouseVoucherHeaderReadModel> BuildHeaders(
        IQueryable<WarehouseVoucher> vouchers,
        IQueryable<WarehouseRequest> requests) =>
        from voucher in vouchers
        join request in requests on voucher.RequestId equals (int?)request.RequestId into requestGroup
        from request in requestGroup.DefaultIfEmpty()
        select new WarehouseVoucherHeaderReadModel
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

    public static IQueryable<WarehouseVoucherHeaderReadModel> ApplyFilters(
        IQueryable<WarehouseVoucherHeaderReadModel> query,
        GetWarehouseVouchersQuery request,
        IQueryable<WarehouseVoucherDetail> details,
        IQueryable<string> sampleProductColourCodes,
        string? normalizedStatus)
    {
        if (request.VoucherType.HasValue)
        {
            query = query.Where(x => x.VoucherType == request.VoucherType.Value);
        }

        if (request.ReqType.HasValue)
        {
            query = query.Where(x => x.ReqType == request.ReqType.Value);
        }

        if (normalizedStatus is not null)
        {
            var statusPattern = PostgresSearchPattern.ContainsLiteral(normalizedStatus);
            query = query.Where(x =>
                x.Status != null &&
                EF.Functions.ILike(
                    x.Status,
                    statusPattern,
                    PostgresSearchPattern.EscapeCharacter));
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate >= request.FromDate.Value.Date);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate < request.ToDate.Value.Date.AddDays(1));
        }

        if (request.NormalizedKeyword is not { } keyword) return query;

        var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
        return query.Where(x =>
            EF.Functions.ILike(x.VoucherCode, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.RequestCode, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.RequestName, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.CodeFromRequest, pattern, PostgresSearchPattern.EscapeCharacter) ||
            details.Any(detail =>
                detail.VoucherId == x.VoucherId &&
                (EF.Functions.ILike(detail.ProductCode, pattern, PostgresSearchPattern.EscapeCharacter) ||
                 EF.Functions.ILike(detail.ProductName, pattern, PostgresSearchPattern.EscapeCharacter) ||
                 (detail.LotNumber != null &&
                  EF.Functions.ILike(detail.LotNumber, pattern, PostgresSearchPattern.EscapeCharacter)) ||
                 sampleProductColourCodes.Contains(detail.ProductCode))));
    }
}
