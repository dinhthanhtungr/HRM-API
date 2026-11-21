using HRM.Application.Features.CRM.CustomerCare.Services.Search;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.CRM.Quotations.Services.Queries;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
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
        private readonly ProductStandardPriceReviewQueryService _standardPriceReviewQueryService;
        private readonly ProductPricingRequestQueryService _pricingRequestQueryService;

        public GetQuotationsQueryHandler(
            ICRMReadDbContext dbContext,
            ICustomerVisibilityService visibilityService,
            ProductStandardPriceReviewQueryService standardPriceReviewQueryService,
            ProductPricingRequestQueryService pricingRequestQueryService)
        {
            _dbContext = dbContext;
            _visibilityService = visibilityService;
            _standardPriceReviewQueryService = standardPriceReviewQueryService;
            _pricingRequestQueryService = pricingRequestQueryService;
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

            var scope = await _visibilityService.BuildReadScopeAsync(cancellationToken);

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

            var smartSearch = SmartSearch.For(query, fields => [
                fields.Code("quotationCode", x => x.ExternalId, "Mã báo giá", "Quotation code"),
                fields.Code("customerCode", x => x.Customer.ExternalId, "Mã KH", "Customer code"),
                fields.Name("customerName", x => x.Customer.CustomerName, "Tên KH", "Customer name"),
                fields.CustomerTaxNumber(x => x.Customer.CompanyId == scope.CompanyId ? x.Customer.TaxNumber : null),
                fields.Name("contactName", x => x.ContactName, "Người liên hệ", "Contact"),
                fields.Codes("colourCode", x => x.Lines.Where(line => line.IsActive && line.ProductNavigation.CompanyId == scope.CompanyId),
                    line => line.ProductNavigation.ColourCode, "ColorCode", "Mã màu"),
                fields.Names("productName", x => x.Lines.Where(line => line.IsActive && line.ProductNavigation.CompanyId == scope.CompanyId),
                    line => line.ProductNavigation.Name, "Tên sản phẩm", "Product name")
            ]);
            query = smartSearch.ApplyRequest(query, request.NormalizedKeyword, request, out var smartKeyword);
            if (smartKeyword is { } keyword)
            {
                query = query.Where(x =>
                    EF.Functions.ILike(x.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike(x.Customer.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike(x.Customer.CustomerName, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    x.Customer.CompanyId == x.CompanyId && EF.Functions.ILike(x.Customer.TaxNumber ?? "", PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    (x.ContactName != null && EF.Functions.ILike(x.ContactName, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||

                    x.Lines.Any(line => line.ProductNavigation.ColourCode != null &&
                        EF.Functions.ILike(line.ProductNavigation.ColourCode, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||

                    x.Lines.Any(line =>
                        line.SampleRequest != null &&
                        EF.Functions.ILike(line.SampleRequest.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)));
            }

            if (await smartSearch.SuggestionsAsync<QuotationListItemDto>(query, request, cancellationToken) is { } suggestions)
                return OperationResult<PagedResult<QuotationListItemDto>>.Ok(suggestions);
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
                    LastSendRejectionAt = x.StatusHistories.Where(h => h.ToStatus == QuotationStatus.SendRejected)
                        .OrderByDescending(h => h.ChangedDate).Select(h => (DateTime?)h.ChangedDate).FirstOrDefault(),
                    LineCount = x.Lines.Count
                })
                .ToPagedResultAsync(
                    request.NormalizedPageNumber,
                    request.NormalizedPageSize,
                    cancellationToken);

            var quotationIds = page.Items.Select(x => x.QuotationId).ToArray();
            var lineProducts = await _dbContext.QuotationLines.AsNoTracking()
                .Where(x => x.IsActive && quotationIds.Contains(x.QuotationId))
                .Select(x => new { x.QuotationId, x.ProductId })
                .ToListAsync(cancellationToken);
            var states = await _standardPriceReviewQueryService.LoadAsync(
                scope.CompanyId,
                lineProducts.Select(x => x.ProductId).Distinct().ToArray(),
                cancellationToken);
            var requests = lineProducts.Count == 0
                ? Array.Empty<ProductPricingRequestRow>()
                : await _pricingRequestQueryService.LoadAsync(
                    scope.CompanyId,
                    lineProducts.Select(x => x.ProductId).Distinct().ToArray(),
                    cancellationToken);
            var requestedAtByLine = requests
                .Where(x => quotationIds.Contains(x.QuotationId))
                .GroupBy(x => (x.QuotationId, x.ProductId))
                .ToDictionary(group => group.Key, group => (DateTime?)group.Max(x => x.RequestedAt));
            foreach (var quotation in page.Items)
            {
                quotation.StandardPriceReview = AggregateReview(
                    lineProducts
                        .Where(x => x.QuotationId == quotation.QuotationId)
                        .Select(x => states.TryGetValue(x.ProductId, out var state)
                            ? ProductStandardPriceStateResolver.ApplySaleRequest(
                                state, requestedAtByLine.GetValueOrDefault((x.QuotationId, x.ProductId)))
                            : null)
                        .Where(x => x is not null)
                        .Cast<ProductStandardPriceStateResult>()
                        .ToArray());
            }

            return OperationResult<PagedResult<QuotationListItemDto>>.Ok(page);
        }

        private static QuotationStandardPriceReviewDto AggregateReview(
            IReadOnlyList<ProductStandardPriceStateResult> states)
        {
            if (states.Count == 0)
            {
                return new QuotationStandardPriceReviewDto
                {
                    State = ProductStandardPriceState.Missing
                };
            }

            var pendingReapprovals = states
                .Where(x => x.State == ProductStandardPriceState.PendingReapproval)
                .ToArray();
            var pendingInitialApprovals = states
                .Where(x => x.State == ProductStandardPriceState.PendingInitialApproval)
                .ToArray();
            var affected = pendingReapprovals.Length > 0
                ? pendingReapprovals
                : pendingInitialApprovals;
            return new QuotationStandardPriceReviewDto
            {
                State = pendingReapprovals.Length > 0
                    ? ProductStandardPriceState.PendingReapproval
                    : pendingInitialApprovals.Length > 0
                        ? ProductStandardPriceState.PendingInitialApproval
                        : ProductStandardPriceState.Active,
                RequiresPricingAction = affected.Length > 0,
                HasFormulaConfirmationPending = pendingReapprovals.Any(x => x.HasFormulaConfirmationPending),
                IsPricingReviewExpired = pendingReapprovals.Any(x => x.IsPricingReviewExpired),
                PricingReviewDueDate = pendingReapprovals
                    .Where(x => x.PricingReviewDueDate.HasValue)
                    .Select(x => x.PricingReviewDueDate)
                    .Min(),
                AffectedLineCount = affected.Length
            };
        }

        private static IQueryable<Quotation> ApplySorting(
            IQueryable<Quotation> query,
            GetQuotationsQuery request)
        {
            return request.NormalizedSortBy?.ToLowerInvariant() switch
            {
                QuotationsSortFeilds.ExternalId => request.SortDescending
                    ? query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.CreatedDate)
                    : query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.CreatedDate),

                QuotationsSortFeilds.TotalAmount => request.SortDescending
                    ? query.OrderByDescending(x => x.TotalAmount).ThenByDescending(x => x.QuotationId)
                    : query.OrderBy(x => x.TotalAmount).ThenByDescending(x => x.QuotationId),

                QuotationsSortFeilds.QuotationDate => request.SortDescending
                    ? query.OrderByDescending(x => x.QuotationDate).ThenByDescending(x => x.QuotationId)
                    : query.OrderBy(x => x.QuotationDate).ThenBy(x => x.QuotationId),

                QuotationsSortFeilds.UpdatedDate => request.SortDescending
                    ? query.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate)
                    : query.OrderBy(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate),

                QuotationsSortFeilds.CreatedDate => request.SortDescending
                    ? query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.UpdatedDate)
                    : query.OrderBy(x => x.CreatedDate).ThenByDescending(x => x.UpdatedDate),

                _ => query
                    .OrderBy(x => x.CreatedDate)
            };
        }
    }

}
