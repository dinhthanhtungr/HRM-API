using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingQueue;

public sealed class GetQuotationPricingQueueQuery
    : PaginationQuery,
      IRequest<OperationResult<PagedResult<QuotationPricingQueueItemDto>>>;
