using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.Tracking;

public sealed record ListQuotationTrackingQuery(int PageNumber) : IRequest<PagedResult<QuotationTrackingDto>?>;
