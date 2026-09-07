using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingQueue;

internal sealed class GetQuotationPricingQueueQueryHandler
    : IRequestHandler<
        GetQuotationPricingQueueQuery,
        OperationResult<PagedResult<QuotationPricingQueueItemDto>>>
{
    private const string QuotationRequestedPayload =
        """{"contentType":"QuotationRequested"}""";

    private readonly ICRMReadDbContext _crmDbContext;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ICurrentUser _currentUser;

    public GetQuotationPricingQueueQueryHandler(
        ICRMReadDbContext crmDbContext,
        IInternalMailDbContext internalMailDbContext,
        ICustomerVisibilityService visibilityService,
        ICurrentUser currentUser)
    {
        _crmDbContext = crmDbContext;
        _internalMailDbContext = internalMailDbContext;
        _visibilityService = visibilityService;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<PagedResult<QuotationPricingQueueItemDto>>> Handle(
        GetQuotationPricingQueueQuery request,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(_currentUser))
        {
            return OperationResult<PagedResult<QuotationPricingQueueItemDto>>.Fail(
                "Only President or Developer can access the quotation pricing queue.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<PagedResult<QuotationPricingQueueItemDto>>.Fail(
                "Current company context is required.");
        }

        var requestRows = await _internalMailDbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.Quotation &&
                x.RelatedId.HasValue)
            .Select(x => new
            {
                QuotationId = x.RelatedId!.Value,
                RequestedAt = x.Messages
                    .Where(message =>
                        !message.IsDeleted &&
                        message.PayloadJson != null &&
                        EF.Functions.JsonContains(
                            message.PayloadJson,
                            QuotationRequestedPayload))
                    .Max(message => (DateTime?)message.SentAt)
            })
            .Where(x => x.RequestedAt.HasValue)
            .ToListAsync(cancellationToken);

        var requestedAtByQuotation = requestRows
            .GroupBy(x => x.QuotationId)
            .ToDictionary(
                x => x.Key,
                x => x.Max(y => y.RequestedAt)!.Value);
        if (requestedAtByQuotation.Count == 0)
        {
            return EmptyResult(request);
        }

        var quotationIds = requestedAtByQuotation.Keys.ToArray();
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotationQuery = _visibilityService.ApplyQuotationVisibility(
                _crmDbContext.Quotations.AsNoTracking(),
                _crmDbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Status == QuotationStatus.Draft &&
                quotationIds.Contains(x.QuotationId));

        if (request.NormalizedKeyword is { } keyword)
        {
            quotationQuery = quotationQuery.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Customer.ExternalId.Contains(keyword) ||
                x.Customer.CustomerName.Contains(keyword) ||
                x.Lines.Any(line =>
                    line.ProductExternalIdSnapshot.Contains(keyword) ||
                    line.ProductNameSnapshot.Contains(keyword)));
        }

        var rows = await quotationQuery
            .Select(x => new QueueRow
            {
                QuotationId = x.QuotationId,
                QuotationExternalId = x.ExternalId,
                CustomerId = x.CustomerId,
                CustomerExternalId = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                SaleEmployeeId = x.SaleEmployeeId,
                SaleEmployeeName = x.SaleEmployee.FullName,
                Currency = x.Currency,
                QuotationDate = x.QuotationDate,
                LineCount = x.Lines.Count,
                PendingPricingLineCount = x.Lines.Count(line =>
                    !_crmDbContext.ProductPricingVersions.Any(pricing =>
                        pricing.CompanyId == companyId &&
                        pricing.ProductId == line.ProductId &&
                        pricing.Currency == ProductPricingSourceRules.StandardPricingCurrency &&
                        pricing.IsActive &&
                        pricing.Status == ProductPricingStatus.Approved))
            })
            .ToListAsync(cancellationToken);

        var visibleQuotationIds = rows.Select(x => x.QuotationId).ToArray();
        var productCodeRows = await _crmDbContext.QuotationLines
            .AsNoTracking()
            .Where(x => visibleQuotationIds.Contains(x.QuotationId))
            .OrderBy(x => x.QuotationId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.QuotationLineId)
            .Select(x => new
            {
                x.QuotationId,
                ProductCode = x.ProductExternalIdSnapshot
            })
            .ToListAsync(cancellationToken);
        var productCodesByQuotation = productCodeRows
            .GroupBy(x => x.QuotationId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)x
                    .Select(y => y.ProductCode)
                    .Distinct()
                    .ToArray());

        var ordered = rows
            .Where(x => x.PendingPricingLineCount > 0)
            .Select(x => new QuotationPricingQueueItemDto
            {
                QuotationId = x.QuotationId,
                QuotationExternalId = x.QuotationExternalId,
                CustomerId = x.CustomerId,
                CustomerExternalId = x.CustomerExternalId,
                CustomerName = x.CustomerName,
                SaleEmployeeId = x.SaleEmployeeId,
                SaleEmployeeName = x.SaleEmployeeName,
                Currency = x.Currency,
                QuotationDate = x.QuotationDate,
                RequestedAt = requestedAtByQuotation[x.QuotationId],
                LineCount = x.LineCount,
                PendingPricingLineCount = x.PendingPricingLineCount,
                ProductCodes = productCodesByQuotation.GetValueOrDefault(x.QuotationId) ?? []
            })
            .OrderByDescending(x => x.RequestedAt)
            .ThenByDescending(x => x.QuotationDate)
            .ThenByDescending(x => x.QuotationId)
            .ToArray();
        var items = ordered
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToArray();

        return OperationResult<PagedResult<QuotationPricingQueueItemDto>>.Ok(
            new PagedResult<QuotationPricingQueueItemDto>(
                items,
                ordered.Length,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }

    private static OperationResult<PagedResult<QuotationPricingQueueItemDto>> EmptyResult(
        GetQuotationPricingQueueQuery request)
        => OperationResult<PagedResult<QuotationPricingQueueItemDto>>.Ok(
            new PagedResult<QuotationPricingQueueItemDto>(
                [],
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));

    private sealed class QueueRow
    {
        public Guid QuotationId { get; init; }
        public string QuotationExternalId { get; init; } = string.Empty;
        public Guid CustomerId { get; init; }
        public string CustomerExternalId { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public Guid SaleEmployeeId { get; init; }
        public string SaleEmployeeName { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
        public DateTime QuotationDate { get; init; }
        public int LineCount { get; init; }
        public int PendingPricingLineCount { get; init; }
    }
}
