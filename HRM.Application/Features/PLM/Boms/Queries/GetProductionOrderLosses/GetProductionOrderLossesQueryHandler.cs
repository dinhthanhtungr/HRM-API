using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetProductionOrderLosses;

internal sealed class GetProductionOrderLossesQueryHandler
    : IRequestHandler<GetProductionOrderLossesQuery, IReadOnlyList<ProductionOrderLossDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetProductionOrderLossesQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProductionOrderLossDto>> Handle(
        GetProductionOrderLossesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty) return [];
        return await _dbContext.MfgProductionOrderLosses.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.MfgProductionOrderId == request.MfgProductionOrderId)
            .OrderBy(x => x.RecordedDate)
            .Select(x => new ProductionOrderLossDto
            {
                MfgProductionOrderLossId = x.MfgProductionOrderLossId,
                SourceManufacturingBomLossRuleId = x.SourceManufacturingBomLossRuleId,
                LossTypeCode = x.LossTypeCodeSnapshot,
                LossTypeName = x.LossTypeNameSnapshot,
                CalculationMethod = x.CalculationMethodSnapshot,
                StageCode = x.StageCodeSnapshot,
                MaterialCode = x.MaterialCodeSnapshot,
                PlannedQuantityKg = x.PlannedQuantityKg,
                ActualQuantityKg = x.ActualQuantityKg,
                EventCount = x.EventCount,
                RecoveredQuantityKg = x.RecoveredQuantityKg,
                IncludeInMaterialRequest = x.IncludeInMaterialRequestSnapshot,
                IsFinalized = x.IsFinalized,
                Note = x.Note
            })
            .ToListAsync(cancellationToken);
    }
}
