using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Dashboard.Dtos;
using HRM.Application.Features.PLM.Dashboard.Shared.Services.Rules;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardPivotHub;

internal sealed class GetDashboardPivotHubQueryHandler
    : IRequestHandler<GetDashboardPivotHubQuery, PlmDashboardPivotHubDto>
{
    private const string SubTotalKey = "subtotal";
    private const string SubTotalLabel = "Sub Total";
    private const string UnknownLabel = "Unknown";
    private static readonly string DeliveredStatus = MerchadiseStatus.Delivered.ToString();
    private static readonly string CancelledOrderStatus = MerchadiseStatus.Cancelled.ToString();

    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetDashboardPivotHubQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PlmDashboardPivotHubDto> Handle(
        GetDashboardPivotHubQuery request,
        CancellationToken cancellationToken)
    {
        var effectiveToDate = (request.ToDate ?? DateTime.Today).Date;
        var effectiveFromDate = (request.FromDate ?? effectiveToDate.AddMonths(-5)).Date;
        var fromMonth = new DateTime(effectiveFromDate.Year, effectiveFromDate.Month, 1);
        var toMonth = new DateTime(effectiveToDate.Year, effectiveToDate.Month, 1);

        var months = Enumerable.Range(0, GetInclusiveMonthCount(fromMonth, toMonth))
            .Select(offset => toMonth.AddMonths(-offset))
            .ToList();

        var queryToDateExclusive = effectiveToDate.AddDays(1);

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
                x.CreatedDate >= effectiveFromDate &&
                x.CreatedDate < queryToDateExclusive &&
                (includeInternalCustomer || x.Customer.ExternalId != PLMRules.InternalCustomerExternalId) &&
                !PLMRules.SampleRequestExcludedStatuses.Contains(x.Status));


        var productionOrders = _dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == scope.CompanyId &&
                x.CreatedDate >= effectiveFromDate &&
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
                x.CreateDate >= effectiveFromDate &&
                x.CreateDate < queryToDateExclusive &&
                x.Customer != null &&
                x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                x.Status != CancelledOrderStatus);




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

        var sampleMetric = await sampleRequests
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Finished = g.Count(x => PLMRules.SampleRequestFinishedStatuses.Contains(x.Status))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var productionMetric = await productionOrders
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Finished = g.Count(x => PLMRules.ProductionOrderFinishedStatuses.Contains(x.Status))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var orderMetric = await merchadiseOrders
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Finished = g.Count(x => x.Status == DeliveredStatus)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var sampleByRequestTypeRows = await sampleRequests
            .GroupBy(x => new
            {
                x.CreatedDate.Year,
                x.CreatedDate.Month,
                GroupKey = string.IsNullOrWhiteSpace(x.RequestType) ? UnknownLabel : x.RequestType
            })
            .Select(g => new PivotSourceRow(
                g.Key.Year,
                g.Key.Month,
                g.Key.GroupKey,
                g.Count(),
                g.Count(x => PLMRules.SampleRequestFinishedStatuses.Contains(x.Status))))
            .ToListAsync(cancellationToken);

        var sampleByStatusRows = await sampleRequests
            .GroupBy(x => new
            {
                x.CreatedDate.Year,
                x.CreatedDate.Month,
                GroupKey = string.IsNullOrWhiteSpace(x.Status) ? UnknownLabel : x.Status
            })
            .Select(g => new PivotSourceRow(
                g.Key.Year,
                g.Key.Month,
                g.Key.GroupKey,
                g.Count(),
                g.Count(x => PLMRules.SampleRequestFinishedStatuses.Contains(x.Status))))
            .ToListAsync(cancellationToken);

        var productionByStatusRows = await productionOrders
            .GroupBy(x => new
            {
                x.CreatedDate.Year,
                x.CreatedDate.Month,
                GroupKey = string.IsNullOrWhiteSpace(x.Status) ? UnknownLabel : x.Status
            })
            .Select(g => new PivotSourceRow(
                g.Key.Year,
                g.Key.Month,
                g.Key.GroupKey,
                g.Count(),
                g.Count(x => PLMRules.ProductionOrderFinishedStatuses.Contains(x.Status))))
            .ToListAsync(cancellationToken);

        var orderByStatusRows = await merchadiseOrders
            .GroupBy(x => new
            {
                x.CreateDate.Year,
                x.CreateDate.Month,
                GroupKey = string.IsNullOrWhiteSpace(x.Status) ? UnknownLabel : x.Status
            })
            .Select(g => new PivotSourceRow(
                g.Key.Year,
                g.Key.Month,
                g.Key.GroupKey,
                g.Count(),
                g.Count(x => x.Status == DeliveredStatus)))
            .ToListAsync(cancellationToken);

        var totalSample = sampleMetric?.Total ?? 0;
        var finishedSample = sampleMetric?.Finished ?? 0;
        var totalProduction = productionMetric?.Total ?? 0;
        var finishedProduction = productionMetric?.Finished ?? 0;
        var totalOrder = orderMetric?.Total ?? 0;
        var finishedOrder = orderMetric?.Finished ?? 0;


        return new PlmDashboardPivotHubDto
        {
            Metrics =
            [
                new PlmDashboardPivotMetricDto
                {
                    Key = "sampleRequests",
                    Label = "Sample Requests",
                    Total = totalSample,
                    Finished = finishedSample,
                    FinishRate = CalculateRate(finishedSample, totalSample)
                },
                new PlmDashboardPivotMetricDto
                {
                    Key = "productionOrders",
                    Label = "Production Orders",
                    Total = totalProduction,
                    Finished = finishedProduction,
                    FinishRate = CalculateRate(finishedProduction, totalProduction)
                },
                new PlmDashboardPivotMetricDto
                {
                    Key = "orders",
                    Label = "Sale Orders",
                    Total = totalOrder,
                    Finished = finishedOrder,
                    FinishRate = CalculateRate(finishedOrder, totalOrder)
                }
            ],
            Tables =
            [
                BuildPivotTable(
                    "sampleRequestsByRequestType",
                    "Sample requests by request type",
                    months,
                    sampleByRequestTypeRows),
                BuildPivotTable(
                    "sampleRequestsByStatus",
                    "Sample requests by status",
                    months,
                    sampleByStatusRows),
                BuildPivotTable(
                    "productionOrdersByStatus",
                    "Production orders by status",
                    months,
                    productionByStatusRows),
                BuildPivotTable(
                    "ordersByStatus",
                    "Sale orders by status",
                    months,
                    orderByStatusRows)
            ]
        };
    }

    private static PlmDashboardPivotTableDto BuildPivotTable(
        string key,
        string title,
        IReadOnlyList<DateTime> months,
        IReadOnlyList<PivotSourceRow> sourceRows)
    {
        var columns = sourceRows
            .Select(x => x.GroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => new PlmDashboardPivotColumnDto
            {
                Key = x,
                Label = x
            })
            .ToList();

        var rows = new List<PlmDashboardPivotRowDto>
        {
            BuildSubtotalRow(sourceRows, columns)
        };

        rows.AddRange(months.Select(month =>
            BuildMonthRow(month, sourceRows, columns)));

        return new PlmDashboardPivotTableDto
        {
            Key = key,
            Title = title,
            Columns = columns,
            Rows = rows
        };
    }

    private static PlmDashboardPivotRowDto BuildSubtotalRow(
        IReadOnlyList<PivotSourceRow> sourceRows,
        IReadOnlyList<PlmDashboardPivotColumnDto> columns)
    {
        var total = sourceRows.Sum(x => x.Total);
        var finished = sourceRows.Sum(x => x.Finished);

        return new PlmDashboardPivotRowDto
        {
            Key = SubTotalKey,
            Label = SubTotalLabel,
            Total = total,
            Finished = finished,
            FinishRate = CalculateRate(finished, total),
            Cells = BuildCells(sourceRows, columns)
        };
    }

    private static PlmDashboardPivotRowDto BuildMonthRow(
        DateTime month,
        IReadOnlyList<PivotSourceRow> sourceRows,
        IReadOnlyList<PlmDashboardPivotColumnDto> columns)
    {
        var monthRows = sourceRows
            .Where(x => x.Year == month.Year && x.Month == month.Month)
            .ToList();

        var total = monthRows.Sum(x => x.Total);
        var finished = monthRows.Sum(x => x.Finished);

        return new PlmDashboardPivotRowDto
        {
            Key = $"{month.Year:D4}-{month.Month:D2}",
            Label = $"{month.Year:D4}-{month.Month:D2}",
            Total = total,
            Finished = finished,
            FinishRate = CalculateRate(finished, total),
            Cells = BuildCells(monthRows, columns)
        };
    }

    private static Dictionary<string, PlmDashboardPivotCellDto> BuildCells(
        IReadOnlyList<PivotSourceRow> rows,
        IReadOnlyList<PlmDashboardPivotColumnDto> columns)
    {
        return columns.ToDictionary(
            x => x.Key,
            x =>
            {
                var matchingRows = rows
                    .Where(row => string.Equals(row.GroupKey, x.Key, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var total = matchingRows.Sum(row => row.Total);
                var finished = matchingRows.Sum(row => row.Finished);

                return new PlmDashboardPivotCellDto
                {
                    Total = total,
                    Finished = finished,
                    FinishRate = CalculateRate(finished, total)
                };
            });
    }

    private static int GetInclusiveMonthCount(DateTime fromMonth, DateTime toMonth)
    {
        if (fromMonth > toMonth)
        {
            return 0;
        }

        return ((toMonth.Year - fromMonth.Year) * 12) + toMonth.Month - fromMonth.Month + 1;
    }

    private static decimal CalculateRate(int value, int total)
    {
        if (total == 0)
        {
            return 0;
        }

        return Math.Round(value * 100m / total, 2);
    }

    private sealed record PivotSourceRow(
        int Year,
        int Month,
        string GroupKey,
        int Total,
        int Finished);
}
