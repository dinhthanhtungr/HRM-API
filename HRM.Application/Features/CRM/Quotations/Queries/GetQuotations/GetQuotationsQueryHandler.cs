using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotations
{
    internal sealed class GetQuotationsQueryHandler
        : IRequestHandler<GetQuotationsQuery, OperationResult<PagedResult<QuotationListItemDto>>>
    {
        private readonly ICRMReadDbContext _dbContext;
        private readonly ICustomerVisibilityService _visibilityService;

        public GetQuotationsQueryHandler(
            ICRMReadDbContext dbContext,
            ICustomerVisibilityService visibilityService)
        {
            _dbContext = dbContext;
            _visibilityService = visibilityService;
        }

        public async Task<OperationResult<PagedResult<QuotationListItemDto>>> Handle(
            GetQuotationsQuery request,
            CancellationToken cancellationToken)
        {
            if (request.From.HasValue && request.To.HasValue && request.From.Value > request.To.Value)
            {
                return OperationResult<PagedResult<QuotationListItemDto>>.Fail(
                    "From cannot be later than To.");
            }

            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var query = _visibilityService.ApplyQuotationVisibility(
                _dbContext.Quotations.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope);

            if (request.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == request.CustomerId.Value);
            }

            if (request.SaleEmployeeId.HasValue)
            {
                query = query.Where(x => x.SaleEmployeeId == request.SaleEmployeeId.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(x => x.Status == request.Status.Value);
            }

            if (request.From.HasValue)
            {
                query = query.Where(x => x.QuotationDate >= request.From.Value);
            }

            if (request.To.HasValue)
            {
                query = query.Where(x => x.QuotationDate <= request.To.Value);
            }

            if (request.NormalizedKeyword is { } keyword)
            {
                query = query.Where(x =>
                    x.ExternalId.Contains(keyword) ||
                    x.Customer.ExternalId.Contains(keyword) ||
                    x.Customer.CustomerName.Contains(keyword) ||
                    (x.ContactName != null && x.ContactName.Contains(keyword)));
            }

            query = ApplySorting(query, request);
            var page = await query
                .Select(x => new QuotationListItemDto
                {
                    QuotationId = x.QuotationId,
                    ExternalId = x.ExternalId,
                    CustomerId = x.CustomerId,
                    CustomerExternalId = x.Customer.ExternalId,
                    CustomerName = x.Customer.CustomerName,
                    SaleEmployeeId = x.SaleEmployeeId,
                    SaleEmployeeName = x.SaleEmployee.FullName,
                    Status = x.Status,
                    Currency = x.Currency,
                    TotalAmount = x.TotalAmount,
                    QuotationDate = x.QuotationDate,
                    ValidUntil = x.ValidUntil,
                    SentDate = x.SentDate,
                    LineCount = x.Lines.Count
                })
                .ToPagedResultAsync(
                    request.NormalizedPageNumber,
                    request.NormalizedPageSize,
                    cancellationToken);

            return OperationResult<PagedResult<QuotationListItemDto>>.Ok(page);
        }

        private static IQueryable<Quotation> ApplySorting(
            IQueryable<Quotation> query,
            GetQuotationsQuery request)
        {
            return request.NormalizedSortBy?.ToLowerInvariant() switch
            {
                "externalid" => request.SortDescending
                    ? query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.QuotationId)
                    : query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.QuotationId),
                "totalamount" => request.SortDescending
                    ? query.OrderByDescending(x => x.TotalAmount).ThenByDescending(x => x.QuotationId)
                    : query.OrderBy(x => x.TotalAmount).ThenByDescending(x => x.QuotationId),
                "status" => request.SortDescending
                    ? query.OrderByDescending(x => x.Status).ThenByDescending(x => x.QuotationId)
                    : query.OrderBy(x => x.Status).ThenByDescending(x => x.QuotationId),
                "quotationdate" => request.SortDescending
                    ? query.OrderByDescending(x => x.QuotationDate).ThenByDescending(x => x.QuotationId)
                    : query.OrderBy(x => x.QuotationDate).ThenBy(x => x.QuotationId),
                _ => query.OrderByDescending(x => x.QuotationDate).ThenByDescending(x => x.QuotationId)
            };
        }
    }

}
