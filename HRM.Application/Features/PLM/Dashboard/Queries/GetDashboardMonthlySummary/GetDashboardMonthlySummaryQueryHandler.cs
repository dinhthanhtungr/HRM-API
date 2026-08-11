using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Dashboard.Dtos;
using HRM.Application.Features.PLM.Dashboard.Shared.Services;
using HRM.Application.Features.PLM.Dashboard.Shared.Services.Rules;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardMonthlySummary;

internal sealed class GetDashboardMonthlySummaryQueryHandler
    : IRequestHandler<GetDashboardMonthlySummaryQuery, PagedResult<PlmMonthlySummaryDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetDashboardMonthlySummaryQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PagedResult<PlmMonthlySummaryDto>> Handle(
        GetDashboardMonthlySummaryQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;

        var effectiveToDate = (request.ToDate ?? DateTime.Today).Date;
        var defaultMonthRange = pageSize - 1;
        var effectiveFromDate = (request.FromDate ?? effectiveToDate.AddMonths(-defaultMonthRange)).Date;

        var fromMonth = new DateTime(effectiveFromDate.Year, effectiveFromDate.Month, 1);
        var toMonth = new DateTime(effectiveToDate.Year, effectiveToDate.Month, 1);

        var totalCount = PlmDashboardMath.GetInclusiveMonthCount(fromMonth, toMonth);
        var pageMonths = Enumerable.Range(0, totalCount)
            .Select(offset => toMonth.AddMonths(-offset))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (pageMonths.Count == 0)
        {
            return new PagedResult<PlmMonthlySummaryDto>(
                new List<PlmMonthlySummaryDto>(),
                totalCount,
                pageNumber,
                pageSize);
        }

        var visibleFromMonth = pageMonths.Min();
        var visibleToExclusive = pageMonths.Max().AddMonths(1);
        var queryFromDate = PlmDashboardMath.Max(effectiveFromDate, visibleFromMonth);
        var queryToDateExclusive = PlmDashboardMath.Min(effectiveToDate.AddDays(1), visibleToExclusive);

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var includeInternalCustomer = PLMRules.ShouldIncludeInternalCustomer(scope);
        var customerQuery = _dbContext.Customers.AsNoTracking();
        var visibleCustomerIds = _visibilityService.ApplyCustomerVisibility(customerQuery, scope)
            .Select(x => x.CustomerId);

        var sampleRequests = _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests.AsNoTracking(),
                customerQuery,
                scope)
            .AsNoTracking()
            .Where(x =>
                x.CreatedDate >= queryFromDate &&
                x.CreatedDate < queryToDateExclusive &&
                (includeInternalCustomer || x.Customer.ExternalId != PLMRules.InternalCustomerExternalId) &&
                !PLMRules.SampleRequestExcludedStatuses.Contains(x.Status));


        var productionOrders = _dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == scope.CompanyId &&
                x.CreatedDate >= queryFromDate &&
                x.CreatedDate < queryToDateExclusive &&
                x.Customer != null &&
                x.CustomerId.HasValue &&
                visibleCustomerIds.Contains(x.CustomerId.Value) &&
                x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                !PLMRules.ProductionOrderExcludedStatuses.Contains(x.Status));

        var merchadiseOrders = _visibilityService.ApplyMerchandiseOrderVisibility(
                _dbContext.MerchandiseOrders.AsNoTracking(),
                customerQuery,
                scope)
            .AsNoTracking()
            .Where(x =>
                x.CreateDate >= queryFromDate &&
                x.CreateDate < queryToDateExclusive &&
                x.Customer != null &&
                x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                !PLMRules.ProductionOrderExcludedStatuses.Contains(x.Status));

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            sampleRequests = sampleRequests.Where(x => x.CompanyId == companyId);
            productionOrders = productionOrders.Where(x => x.CompanyId == companyId);
            merchadiseOrders = merchadiseOrders.Where(x => x.CompanyId == companyId);
        }

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            sampleRequests = sampleRequests.Where(x => x.ProductId == productId);
            productionOrders = productionOrders.Where(x => x.ProductId == productId);
            merchadiseOrders = merchadiseOrders.Where(x =>
                x.MerchandiseOrderDetails.Any(detail =>
                    detail.IsActive &&
                    detail.ProductId == productId));
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            sampleRequests = sampleRequests.Where(x => x.CustomerId == customerId);
            productionOrders = productionOrders.Where(x => x.CustomerId == customerId);
            merchadiseOrders = merchadiseOrders.Where(x => x.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            sampleRequests = sampleRequests.Where(x => x.Status == status);
            productionOrders = productionOrders.Where(x => x.Status == status);
            merchadiseOrders = merchadiseOrders.Where(x => x.Status == status);
        }

        var sampleRows = await sampleRequests
            .GroupBy(x => new { x.CreatedDate.Year, x.CreatedDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Finished = g.Count(x => PLMRules.SampleRequestFinishedStatuses.Contains(x.Status))
            })
            .ToListAsync(cancellationToken);

        var productionRows = await productionOrders
            .GroupBy(x => new { x.CreatedDate.Year, x.CreatedDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Finished = g.Count(x => PLMRules.ProductionOrderFinishedStatuses.Contains(x.Status))
            })
            .ToListAsync(cancellationToken);

        var orderRows = await merchadiseOrders
            .GroupBy(x => new { x.CreateDate.Year, x.CreateDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Finished = g.Count(x => MerchadiseStatus.Delivered.ToString().Contains(x.Status))
            })
            .ToListAsync(cancellationToken);

        var result = pageMonths.ToDictionary(
            month => (month.Year, month.Month),
            month => new PlmMonthlySummaryDto
            {
                Year = month.Year,
                Month = month.Month,
                MonthKey = $"{month.Year:D4}-{month.Month:D2}"
            });

        foreach (var row in sampleRows)
        {
            if (!result.TryGetValue((row.Year, row.Month), out var item))
            {
                continue;
            }

            item.SampleRequests = row.Total;
            item.SampleRequestFinish = row.Finished;
            item.SampleRequestFinishRate = PlmDashboardMath.CalculateRate(row.Finished, row.Total);
        }

        foreach (var row in productionRows)
        {
            if (!result.TryGetValue((row.Year, row.Month), out var item))
            {
                continue;
            }

            item.ProductionOrders = row.Total;
            item.ProductionOrderFinish = row.Finished;
            item.ProductionOrderFinishRate = PlmDashboardMath.CalculateRate(row.Finished, row.Total);
        }

        foreach (var row in orderRows)
        {
            if (!result.TryGetValue((row.Year, row.Month), out var item))
            {
                continue;
            }

            item.Orders = row.Total;
            item.OrderFinish = row.Finished;
            item.OrderFinishRate = PlmDashboardMath.CalculateRate(row.Finished, row.Total);
        }

        return new PagedResult<PlmMonthlySummaryDto>(
            pageMonths.Select(month => result[(month.Year, month.Month)]).ToList(),
            totalCount,
            pageNumber,
            pageSize);
    }
}
