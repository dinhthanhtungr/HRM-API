using System.Globalization;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Dashboard.Dtos;
using HRM.Application.Features.PLM.Dashboard.Shared.Services.Rules;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardDrilldown;

internal sealed class GetDashboardDrilldownQueryHandler
    : IRequestHandler<GetDashboardDrilldownQuery, PagedResult<PlmDashboardDrilldownItemDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetDashboardDrilldownQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PagedResult<PlmDashboardDrilldownItemDto>> Handle(
        GetDashboardDrilldownQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = ResolveDateRange(request);
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);

        if (PLMRules.IsProductionOrderSource(request.NormalizedSource))
        {
            return await GetProductionOrdersAsync(request, dateRange, scope, cancellationToken);
        }

        if (PLMRules.IsOrderSource(request.NormalizedSource))
        {
            return await GetOrdersAsync(request, dateRange, scope, cancellationToken);
        }

        return await GetSampleRequestsAsync(request, dateRange, scope, cancellationToken);
    }

    private async Task<PagedResult<PlmDashboardDrilldownItemDto>> GetSampleRequestsAsync(
        GetDashboardDrilldownQuery request,
        DateRange dateRange,
        ViewerScope scope,
        CancellationToken cancellationToken)
    {
        var includeInternalCustomer = PLMRules.ShouldIncludeInternalCustomer(scope);
        var customerQuery = _dbContext.Customers.AsNoTracking();
        var query = _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests.AsNoTracking(),
                customerQuery,
                scope)
            .AsNoTracking()
            .Where(x =>
                (includeInternalCustomer || x.Customer.ExternalId != PLMRules.InternalCustomerExternalId) &&
                !PLMRules.SampleRequestExcludedStatuses.Contains(x.Status))
            .AsQueryable();

        if (dateRange.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate >= dateRange.FromDate.Value);
        }

        if (dateRange.ToDateExclusive.HasValue)
        {
            query = query.Where(x => x.CreatedDate < dateRange.ToDateExclusive.Value);
        }

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            query = query.Where(x => x.ProductId == productId);
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }
        query = ApplyCompletionFilter(query, request.NormalizedCompletion);

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Status.Contains(keyword) ||
                x.RequestType.Contains(keyword) ||
                x.Customer.CustomerName.Contains(keyword) ||
                x.Customer.ExternalId.Contains(keyword) ||
                (x.Product.Name ?? string.Empty).Contains(keyword) ||
                (x.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                (x.Product.Code ?? string.Empty).Contains(keyword) ||
                (x.Formula != null && EF.Functions.ILike(x.Formula.ExternalId, $"%{keyword}%")) ||
                x.ManagerByNavigation.FullName.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (request.NormalizedPageNumber - 1) * request.NormalizedPageSize;

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.SampleRequestId)
            .Skip(skip)
            .Take(request.NormalizedPageSize)
            .Select(x => new PlmDashboardDrilldownItemDto
            {
                SourceType = PLMRules.SampleRequestsSource,
                RecordId = x.SampleRequestId,
                ExternalId = x.ExternalId,
                RequesterName = x.CreatedByNavigation != null ? x.CreatedByNavigation.FullName : null,
                Status = x.Status,
                Title = x.Product.Name,
                ModelName = x.Product.ColourCode ?? x.Product.Code ?? x.Product.Name,
                Quantity = x.SampleQuantity ?? x.ExpectedQuantity,
                CreatedDate = x.CreatedDate,
                RequestedCompletionDate = x.ExpectedDeliveryDate ?? x.RequestDeliveryDate,
                CompletedDate = PLMRules.SampleRequestFinishedStatuses.Contains(x.Status)
                    ? x.RealDeliveryDate
                    : null,
                ManagerName = x.Product != null
                    ? (x.Product.CreatedByNavigation != null
                        ? x.Product.CreatedByNavigation.FullName
                        : "-")
                    : null,
            })
            .ToListAsync(cancellationToken);

        AssignRowNumbers(items, skip);

        return new PagedResult<PlmDashboardDrilldownItemDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private async Task<PagedResult<PlmDashboardDrilldownItemDto>> GetProductionOrdersAsync(
        GetDashboardDrilldownQuery request,
        DateRange dateRange,
        ViewerScope scope,
        CancellationToken cancellationToken)
    {
        var customerQuery = _dbContext.Customers.AsNoTracking();
        var visibleCustomerIds = _visibilityService.ApplyCustomerVisibility(customerQuery, scope)
            .Select(x => x.CustomerId);
        var query = _dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == scope.CompanyId &&
                x.CustomerId.HasValue &&
                visibleCustomerIds.Contains(x.CustomerId.Value) &&
                x.Customer != null && x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                !PLMRules.ProductionOrderExcludedStatuses.Contains(x.Status))
            .AsQueryable();

        if (dateRange.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate >= dateRange.FromDate.Value);
        }

        if (dateRange.ToDateExclusive.HasValue)
        {
            query = query.Where(x => x.CreatedDate < dateRange.ToDateExclusive.Value);
        }

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            query = query.Where(x => x.ProductId == productId);
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        query = ApplyCompletionFilter(query, request.NormalizedCompletion);
        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Status.Contains(keyword) ||
                (x.CustomerNameSnapshot ?? string.Empty).Contains(keyword) ||
                (x.CustomerExternalIdSnapshot ?? string.Empty).Contains(keyword) ||
                (x.ProductNameSnapshot ?? string.Empty).Contains(keyword) ||
                (x.ProductExternalIdSnapshot ?? string.Empty).Contains(keyword) ||
                (x.Requirement ?? string.Empty).Contains(keyword) ||
                (x.CreatedByNavigation != null &&
                    x.CreatedByNavigation.FullName.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (request.NormalizedPageNumber - 1) * request.NormalizedPageSize;

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.MfgProductionOrderId)
            .Skip(skip)
            .Take(request.NormalizedPageSize)
            .Select(x => new PlmDashboardDrilldownItemDto
            {
                SourceType = PLMRules.ProductionOrdersSource,
                RecordId = x.MfgProductionOrderId,
                ExternalId = x.ExternalId,
                RequesterName = x.CreatedByNavigation != null ? x.CreatedByNavigation.FullName : null,
                Status = x.Status,
                Title = x.Requirement ?? x.ProductNameSnapshot ?? x.Product.Name,
                ModelName = x.ProductNameSnapshot ?? x.ProductExternalIdSnapshot ?? x.Product.Name,
                Quantity = (double)x.TotalQuantityRequest,
                CreatedDate = x.CreatedDate,
                RequestedCompletionDate = x.ExpectedDate ?? x.RequiredDate,
                CompletedDate = PLMRules.ProductionOrderFinishedStatuses.Contains(x.Status)
                    ? x.UpdatedDate
                    : null,
                ManagerName = x.UpdatedByNavigation != null
                    ? x.UpdatedByNavigation.FullName
                    : x.CreatedByNavigation != null
                        ? x.CreatedByNavigation.FullName
                        : null
            })
            .ToListAsync(cancellationToken);

        AssignRowNumbers(items, skip);

        return new PagedResult<PlmDashboardDrilldownItemDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private async Task<PagedResult<PlmDashboardDrilldownItemDto>> GetOrdersAsync(
        GetDashboardDrilldownQuery request,
        DateRange dateRange,
        ViewerScope scope,
        CancellationToken cancellationToken)
    {
        var customerQuery = _dbContext.Customers.AsNoTracking();
        var query = _visibilityService.ApplyMerchandiseOrderVisibility(
                _dbContext.MerchandiseOrders.AsNoTracking(),
                customerQuery,
                scope)
            .AsNoTracking()
            .Where(x =>
                x.Customer != null &&
                x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                !PLMRules.OrderExcludedStatuses.Contains(x.Status))
            .AsQueryable();

        if (dateRange.FromDate.HasValue)
        {
            query = query.Where(x => x.CreateDate >= dateRange.FromDate.Value);
        }

        if (dateRange.ToDateExclusive.HasValue)
        {
            query = query.Where(x => x.CreateDate < dateRange.ToDateExclusive.Value);
        }

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            query = query.Where(x =>
                x.MerchandiseOrderDetails.Any(detail =>
                    detail.IsActive &&
                    detail.ProductId == productId));
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        query = ApplyCompletionFilter(query, request.NormalizedCompletion);

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Status.Contains(keyword) ||
                x.PONo.Contains(keyword) ||
                x.CustomerNameSnapshot.Contains(keyword) ||
                x.CustomerExternalIdSnapshot.Contains(keyword) ||
                x.ManagerByNameSnapshot.Contains(keyword) ||
                x.MerchandiseOrderDetails.Any(detail =>
                    detail.ProductExternalIdSnapshot.Contains(keyword) ||
                    detail.ProductNameSnapshot.Contains(keyword) ||
                    EF.Functions.ILike(detail.FormulaExternalIdSnapshot, $"%{keyword}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (request.NormalizedPageNumber - 1) * request.NormalizedPageSize;

        var items = await query
            .OrderByDescending(x => x.CreateDate)
            .ThenByDescending(x => x.MerchandiseOrderId)
            .Skip(skip)
            .Take(request.NormalizedPageSize)
            .Select(x => new PlmDashboardDrilldownItemDto
            {
                SourceType = PLMRules.OrdersSource,
                RecordId = x.MerchandiseOrderId,
                ExternalId = x.ExternalId,
                RequesterName = x.CreatedByNavigation != null ? x.CreatedByNavigation.FullName : null,
                Status = x.Status,
                Title = x.PONo,
                ModelName = x.CustomerNameSnapshot,
                Quantity = (double)(x.MerchandiseOrderDetails
                    .Where(detail => detail.IsActive)
                    .Sum(detail => (decimal?)detail.ExpectedQuantity) ?? 0m),
                CreatedDate = x.CreateDate,
                RequestedCompletionDate = x.MerchandiseOrderDetails
                    .Where(detail => detail.IsActive)
                    .Min(detail => (DateTime?)detail.ExpectedDeliveryDate),
                CompletedDate = PLMRules.OrderFinishedStatuses.Contains(x.Status)
                    ? x.UpdatedDate
                    : null,
                ManagerName = !string.IsNullOrWhiteSpace(x.ManagerByNameSnapshot)
                    ? x.ManagerByNameSnapshot
                    : x.ManagerBy != null
                        ? x.ManagerBy.FullName
                        : null
            })
            .ToListAsync(cancellationToken);

        AssignRowNumbers(items, skip);

        return new PagedResult<PlmDashboardDrilldownItemDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static IQueryable<SampleRequest> ApplyCompletionFilter(
        IQueryable<SampleRequest> query,
        string completion)
    {
        if (completion.Equals(PLMRules.FinishedCompletion, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => PLMRules.SampleRequestFinishedStatuses.Contains(x.Status));
        }

        if (completion.Equals(PLMRules.UnfinishedCompletion, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => !PLMRules.SampleRequestFinishedStatuses.Contains(x.Status));
        }

        return query;
    }

    private static IQueryable<MerchandiseOrder> ApplyCompletionFilter(
        IQueryable<MerchandiseOrder> query,
        string completion)
    {
        if (completion.Equals(PLMRules.FinishedCompletion, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => PLMRules.OrderFinishedStatuses.Contains(x.Status));
        }

        if (completion.Equals(PLMRules.UnfinishedCompletion, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => !PLMRules.OrderFinishedStatuses.Contains(x.Status));
        }

        return query;
    }

    private static IQueryable<MfgProductionOrder> ApplyCompletionFilter(
        IQueryable<MfgProductionOrder> query,
        string completion)
    {
        if (completion.Equals(PLMRules.FinishedCompletion, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => PLMRules.ProductionOrderFinishedStatuses.Contains(x.Status));
        }

        if (completion.Equals(PLMRules.UnfinishedCompletion, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => !PLMRules.ProductionOrderFinishedStatuses.Contains(x.Status));
        }

        return query;
    }

    private static void AssignRowNumbers(
        IReadOnlyList<PlmDashboardDrilldownItemDto> items,
        int skip)
    {
        for (var index = 0; index < items.Count; index++)
        {
            items[index].No = skip + index + 1;
        }
    }

    private static DateRange ResolveDateRange(GetDashboardDrilldownQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.MonthKey) &&
            DateTime.TryParseExact(
                request.MonthKey.Trim(),
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month))
        {
            return new DateRange(month, month.AddMonths(1));
        }

        return new DateRange(
            request.FromDate?.Date,
            request.ToDate?.Date.AddDays(1));
    }

    private sealed record DateRange(DateTime? FromDate, DateTime? ToDateExclusive);
}
