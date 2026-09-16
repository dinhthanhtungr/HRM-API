using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetProductionMaterialRequirements;

internal sealed class GetProductionMaterialRequirementsQueryHandler
    : IRequestHandler<GetProductionMaterialRequirementsQuery, OperationResult<IReadOnlyList<ProductionMaterialRequirementDto>>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetProductionMaterialRequirementsQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<IReadOnlyList<ProductionMaterialRequirementDto>>> Handle(
        GetProductionMaterialRequirementsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<IReadOnlyList<ProductionMaterialRequirementDto>>.Fail("Current company is invalid.");

        var order = await _dbContext.MfgProductionOrders.AsNoTracking()
            .Where(x => x.MfgProductionOrderId == request.MfgProductionOrderId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new { x.MfgProductionOrderId, x.TotalQuantityRequest })
            .FirstOrDefaultAsync(cancellationToken);
        if (order is null)
            return OperationResult<IReadOnlyList<ProductionMaterialRequirementDto>>.Fail("Production order was not found.");

        var formulaId = await _dbContext.ProductionSelectVersions.AsNoTracking()
            .Where(x => x.MfgProductionOrderId == order.MfgProductionOrderId &&
                        x.CompanyId == companyId && x.ValidFrom != null && x.ValidTo == null)
            .Select(x => x.ManufacturingFormulaId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!formulaId.HasValue)
            return OperationResult<IReadOnlyList<ProductionMaterialRequirementDto>>.Fail("Production order has no selected formula.");

        var materials = await _dbContext.ManufacturingFormulaMaterials.AsNoTracking()
            .Where(x => x.ManufacturingFormulaId == formulaId && x.IsActive && x.itemType == ItemType.Material)
            .Select(x => new
            {
                x.MaterialId,
                Code = x.MaterialExternalIdSnapshot ?? string.Empty,
                Name = x.MaterialNameSnapshot,
                BaseQuantity = x.Quantity * order.TotalQuantityRequest
            })
            .ToListAsync(cancellationToken);
        var lossRows = await _dbContext.MfgProductionOrderLosses.AsNoTracking()
            .Where(x => x.CompanyId == companyId &&
                        x.MfgProductionOrderId == order.MfgProductionOrderId &&
                        x.IncludeInMaterialRequestSnapshot &&
                        x.MaterialCodeSnapshot != null)
            .GroupBy(x => x.MaterialCodeSnapshot!)
            .Select(x => new { Code = x.Key, Quantity = x.Sum(row => row.PlannedQuantityKg) })
            .ToListAsync(cancellationToken);
        var losses = lossRows.ToDictionary(x => x.Code, x => x.Quantity, StringComparer.OrdinalIgnoreCase);

        var result = materials
            .GroupBy(x => new { x.MaterialId, x.Code, x.Name })
            .Select(group =>
        {
            losses.TryGetValue(group.Key.Code, out var lossQuantity);
            var baseQuantity = decimal.Round(group.Sum(x => x.BaseQuantity), 3, MidpointRounding.AwayFromZero);
            return new ProductionMaterialRequirementDto
            {
                MaterialId = group.Key.MaterialId,
                MaterialCode = group.Key.Code,
                MaterialName = group.Key.Name,
                BaseQuantityKg = baseQuantity,
                PlannedLossQuantityKg = lossQuantity,
                TotalRequiredQuantityKg = decimal.Round(baseQuantity + lossQuantity, 3, MidpointRounding.AwayFromZero)
            };
        })
            .OrderBy(x => x.MaterialCode)
            .ToList();

        return OperationResult<IReadOnlyList<ProductionMaterialRequirementDto>>.Ok(result);
    }
}
