using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ProductionOrders.Queries.CheckProductInProduction;

internal sealed class CheckProductInProductionQueryHandler
    : IRequestHandler<CheckProductInProductionQuery, ProductInProductionDto>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CheckProductInProductionQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ProductInProductionDto> Handle(
        CheckProductInProductionQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return new ProductInProductionDto();
        }

        var matchingOrders = await _dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(order =>
                order.CompanyId == companyId &&
                order.ProductId == request.ProductId &&
                order.IsActive &&
                ProductionOrderStatusRules.InProductionStatuses.Contains(order.Status))
            .Select(order => new
            {
                ManufacturingFormulaExternalId = order.ProductionSelectVersions
                    .Where(version =>
                        version.CompanyId == companyId &&
                        version.ValidFrom != null &&
                        version.ValidTo == null &&
                        version.ManufacturingFormula != null &&
                        version.ManufacturingFormula.CompanyId == companyId)
                    .OrderByDescending(version => version.ValidFrom)
                    .Select(version => version.ManufacturingFormula!.ExternalId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new ProductInProductionDto
        {
            IsInProduction = matchingOrders.Count > 0,
            ManufacturingFormulaExternalIds = matchingOrders
                .Select(order => order.ManufacturingFormulaExternalId)
                .Where(externalId => !string.IsNullOrWhiteSpace(externalId))
                .Select(externalId => externalId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(externalId => externalId, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
}
