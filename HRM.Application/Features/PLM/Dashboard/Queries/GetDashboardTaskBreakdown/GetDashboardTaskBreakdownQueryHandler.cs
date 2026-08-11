using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Dashboard.Dtos;
using HRM.Application.Features.PLM.Dashboard.Shared.Services.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardTaskBreakdown
{
    internal sealed class GetDashboardTaskBreakdownQueryHandler
        : IRequestHandler<GetDashboardTaskBreakdownQuery, IReadOnlyList<PlmTaskBreakdownDto>>
    {
        private readonly IPLMReadDbContext _dbContext;
        private readonly ICustomerVisibilityService _visibilityService;

        public GetDashboardTaskBreakdownQueryHandler(
            IPLMReadDbContext dbContext,
            ICustomerVisibilityService visibilityService)
        {
            _dbContext = dbContext;
            _visibilityService = visibilityService;
        }

        public async Task<IReadOnlyList<PlmTaskBreakdownDto>> Handle(
            GetDashboardTaskBreakdownQuery request,
            CancellationToken cancellationToken)
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var includeInternalCustomer = PLMRules.ShouldIncludeInternalCustomer(scope);
            var customerQuery = _dbContext.Customers.AsNoTracking();

            var sampleRequests = _visibilityService.ApplySampleRequestVisibility(
                    _dbContext.SampleRequests.AsNoTracking(),
                    customerQuery,
                    scope)
                .AsNoTracking()
                .Where(x =>
                    (includeInternalCustomer || x.Customer.ExternalId != PLMRules.InternalCustomerExternalId) &&
                    !PLMRules.SampleRequestExcludedStatuses.Contains(x.Status))
                .AsQueryable();

            var orders = _visibilityService.ApplyMerchandiseOrderVisibility(
                    _dbContext.MerchandiseOrders.AsNoTracking(),
                    customerQuery,
                    scope)
                .AsNoTracking()
                .Where(x =>
                    x.Customer != null &&
                    x.Customer.ExternalId != PLMRules.InternalCustomerExternalId &&
                    !PLMRules.OrderExcludedStatuses.Contains(x.Status))
                .AsQueryable();

            if (request.CompanyId.HasValue)
            {
                sampleRequests = sampleRequests.Where(x => x.CompanyId == request.CompanyId.Value);
                orders = orders.Where(x => x.CompanyId == request.CompanyId.Value);
            }

            if (request.ProductId.HasValue)
            {
                sampleRequests = sampleRequests.Where(x => x.ProductId == request.ProductId.Value);
                orders = orders.Where(x =>
                    x.MerchandiseOrderDetails.Any(detail =>
                        detail.IsActive &&
                        detail.ProductId == request.ProductId.Value));
            }

            if (request.CustomerId.HasValue)
            {
                sampleRequests = sampleRequests.Where(x => x.CustomerId == request.CustomerId.Value);
                orders = orders.Where(x => x.CustomerId == request.CustomerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                sampleRequests = sampleRequests.Where(x => x.Status == request.Status);
                orders = orders.Where(x => x.Status == request.Status);
            }

            if (request.FromDate.HasValue)
            {
                var fromDate = request.FromDate.Value.Date;
                sampleRequests = sampleRequests.Where(x => x.CreatedDate >= fromDate);
                orders = orders.Where(x => x.CreateDate >= fromDate);
            }

            if (request.ToDate.HasValue)
            {
                var toDate = request.ToDate.Value.Date.AddDays(1);
                sampleRequests = sampleRequests.Where(x => x.CreatedDate < toDate);
                orders = orders.Where(x => x.CreateDate < toDate);
            }

            var sampleRows = await sampleRequests
                .GroupBy(x => string.IsNullOrWhiteSpace(x.RequestType) ? "Unknown" : x.RequestType)
                .Select(g => new
                {
                    TaskType = g.Key,
                    RequestCount = g.Count(),
                    ProductCount = g.Select(x => x.ProductId).Distinct().Count(),
                    ExpectedQuantity = g.Sum(x => x.ExpectedQuantity ?? 0d),
                    SampleQuantity = g.Sum(x => x.SampleQuantity ?? 0d),
                    FormulaAssignedCount = g.Count(x => x.FormulaId != null)
                })
                .OrderByDescending(x => x.RequestCount)
                .ToListAsync(cancellationToken);

            var orderRows = await orders
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Status) ? "Unknown" : x.Status)
                .Select(g => new
                {
                    TaskType = "Sale Order - " + g.Key,
                    RequestCount = g.Count(),
                    ProductCount = g.SelectMany(x => x.MerchandiseOrderDetails
                            .Where(detail => detail.IsActive)
                            .Select(detail => detail.ProductId))
                        .Distinct()
                        .Count(),
                    ExpectedQuantity = g.SelectMany(x => x.MerchandiseOrderDetails
                            .Where(detail => detail.IsActive))
                        .Sum(detail => (decimal?)detail.ExpectedQuantity) ?? 0m,
                    SampleQuantity = 0m,
                    FormulaAssignedCount = g.SelectMany(x => x.MerchandiseOrderDetails
                            .Where(detail => detail.IsActive))
                        .Count(detail => detail.FormulaId != Guid.Empty)
                })
                .OrderByDescending(x => x.RequestCount)
                .ToListAsync(cancellationToken);

            return sampleRows
                .Select(x => new PlmTaskBreakdownDto
                {
                    TaskType = x.TaskType,
                    RequestCount = x.RequestCount,
                    ProductCount = x.ProductCount,
                    ExpectedQuantity = Convert.ToDecimal(x.ExpectedQuantity),
                    SampleQuantity = Convert.ToDecimal(x.SampleQuantity),
                    FormulaAssignedCount = x.FormulaAssignedCount
                })
                .Concat(orderRows.Select(x => new PlmTaskBreakdownDto
                {
                    TaskType = x.TaskType,
                    RequestCount = x.RequestCount,
                    ProductCount = x.ProductCount,
                    ExpectedQuantity = x.ExpectedQuantity,
                    SampleQuantity = x.SampleQuantity,
                    FormulaAssignedCount = x.FormulaAssignedCount
                }))
                .OrderByDescending(x => x.RequestCount)
                .ToList();
        }
    }
}
