using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Enums.WareHouses;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseVouchers;

/// <summary>Returns company-scoped voucher history that originated from a warehouse request.</summary>
public sealed class GetWarehouseVouchersQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<WarehouseVoucherListItemDto>>>
{
    public int? VoucherType { get; init; }
    public WareHouseRequestType? ReqType { get; init; }
    public string? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}
