using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Dashboard.Dtos;
using HRM.Application.Features.PLM.Dashboard.Shared.Services;
using HRM.Application.Features.PLM.Dashboard.Shared.Services.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardSummary;

internal sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, PlmDashboardSummaryDto>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetDashboardSummaryQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PlmDashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
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
            .AsQueryable()
            .Where(x =>
                (includeInternalCustomer || x.Customer.ExternalId != PLMRules.InternalCustomerExternalId) &&
                !PLMRules.SampleRequestExcludedStatuses.Contains(x.Status));

        var productionOrders = _dbContext.MfgProductionOrders
            .AsNoTracking()
            .AsQueryable()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == scope.CompanyId &&
                x.Customer != null &&
                x.CustomerId.HasValue &&
                visibleCustomerIds.Contains(x.CustomerId.Value) &&
                x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                !PLMRules.ProductionOrderExcludedStatuses.Contains(x.Status));

        var orders = _visibilityService.ApplyMerchandiseOrderVisibility(
                _dbContext.MerchandiseOrders.AsNoTracking(),
                customerQuery,
                scope)
            .AsNoTracking()
            .AsQueryable()
            .Where(x =>
                x.Customer != null &&
                x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                !PLMRules.OrderExcludedStatuses.Contains(x.Status));


        var fromDate = request.FromDate?.Date
            ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var toDate = request.ToDate?.Date.AddDays(1)
            ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            sampleRequests = sampleRequests.Where(x => x.CompanyId == companyId);
            productionOrders = productionOrders.Where(x => x.CompanyId == companyId);
            orders = orders.Where(x => x.CompanyId == companyId);
        }

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            sampleRequests = sampleRequests.Where(x => x.ProductId == productId);
            productionOrders = productionOrders.Where(x => x.ProductId == productId);
            orders = orders.Where(x =>
                x.MerchandiseOrderDetails.Any(detail =>
                    detail.IsActive &&
                    detail.ProductId == productId));
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            sampleRequests = sampleRequests.Where(x => x.CustomerId == customerId);
            productionOrders = productionOrders.Where(x => x.CustomerId == customerId);
            orders = orders.Where(x => x.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            sampleRequests = sampleRequests.Where(x => x.Status == status);
            productionOrders = productionOrders.Where(x => x.Status == status);
            orders = orders.Where(x => x.Status == status);
        }

        if (request.FromDate.HasValue)
        {
            sampleRequests = sampleRequests.Where(x => x.CreatedDate >= fromDate);
            productionOrders = productionOrders.Where(x => x.CreatedDate >= fromDate);
            orders = orders.Where(x => x.CreateDate >= fromDate);
        }

        if (request.ToDate.HasValue)
        {
            sampleRequests = sampleRequests.Where(x => x.CreatedDate < toDate);
            productionOrders = productionOrders.Where(x => x.CreatedDate < toDate);
            orders = orders.Where(x => x.CreateDate < toDate);
        }

        var productionStats = await productionOrders
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Finished = g.Count(x => PLMRules.ProductionOrderFinishedStatuses.Contains(x.Status))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var sampleStats = await sampleRequests
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Finished = g.Count(x => PLMRules.SampleRequestFinishedStatuses.Contains(x.Status))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var orderStats = await orders
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Finished = g.Count(x => PLMRules.OrderFinishedStatuses.Contains(x.Status))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalSampleRequests = sampleStats?.Total ?? 0;
        var totalSampleRequestFinish = sampleStats?.Finished ?? 0;
        var totalProductionOrders = productionStats?.Total ?? 0;
        var totalProductionOrderFinish = productionStats?.Finished ?? 0;
        var totalOrders = orderStats?.Total ?? 0;
        var totalOrderFinish = orderStats?.Finished ?? 0;

        return new PlmDashboardSummaryDto
        {
            TotalSampleRequests = totalSampleRequests,
            TotalSampleRequestFinish = totalSampleRequestFinish,
            TotalProductionOrders = totalProductionOrders,
            TotalProductionOrderFinish = totalProductionOrderFinish,
            TotalOrders = totalOrders,
            TotalOrderFinish = totalOrderFinish,
            SampleRequestFinishRate = PlmDashboardMath.CalculateRate(totalSampleRequestFinish, totalSampleRequests),
            ProductionOrderFinishRate = PlmDashboardMath.CalculateRate(totalProductionOrderFinish, totalProductionOrders),
            OrderFinishRate = PlmDashboardMath.CalculateRate(totalOrderFinish, totalOrders)
        };
    }
}
