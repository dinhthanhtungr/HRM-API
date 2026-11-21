using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;

public sealed record ListSaleOrderTrackingQuery(int PageNumber) : IRequest<PagedResult<SaleOrderTrackingDto>?>;
