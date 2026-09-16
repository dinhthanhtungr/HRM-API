using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Audits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.InitializeProductionOrderLosses;

internal sealed class InitializeProductionOrderLossesCommandHandler
    : IRequestHandler<InitializeProductionOrderLossesCommand, OperationResult<IReadOnlyList<ProductionOrderLossDto>>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public InitializeProductionOrderLossesCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<IReadOnlyList<ProductionOrderLossDto>>> Handle(
        InitializeProductionOrderLossesCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<ProductionOrderLossDto>>.Fail("Current company or employee is invalid.");
        }

        var order = await _dbContext.MfgProductionOrders.AsNoTracking()
            .Where(x => x.MfgProductionOrderId == command.MfgProductionOrderId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new { x.MfgProductionOrderId, x.TotalQuantityRequest, x.NumOfBatches })
            .FirstOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return OperationResult<IReadOnlyList<ProductionOrderLossDto>>.Fail("Production order was not found.");
        }

        var sourceBomVersionId = await _dbContext.ProductionSelectVersions.AsNoTracking()
            .Where(x => x.MfgProductionOrderId == order.MfgProductionOrderId &&
                        x.CompanyId == companyId && x.ValidFrom != null && x.ValidTo == null)
            .Select(x => x.ManufacturingFormula!.SourceBomVersionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!sourceBomVersionId.HasValue)
        {
            return OperationResult<IReadOnlyList<ProductionOrderLossDto>>.Fail(
                "The selected manufacturing formula is not linked to a Manufacturing BOM.");
        }

        var rules = await _dbContext.ManufacturingBomLossRules.AsNoTracking()
            .Where(x => x.BomVersionId == sourceBomVersionId && x.IsActive)
            .OrderBy(x => x.SequenceNo)
            .Select(x => new
            {
                Rule = x,
                LossTypeCode = x.LossType.Code,
                LossTypeName = x.LossType.Name,
                StageCode = x.ManufacturingStage != null ? x.ManufacturingStage.Code : null,
                MaterialCode = x.BomVersionItem != null ? x.BomVersionItem.MaterialExternalIdSnapshot : null,
                MaterialQuantityPerOutput = x.BomVersionItem != null
                    ? x.BomVersionItem.Quantity / x.BomVersion.BaseOutputQuantity
                    : (decimal?)null,
                StageQuantityPerOutput = x.ManufacturingStage != null
                    ? x.ManufacturingStage.Items.Sum(item => item.Quantity) / x.BomVersion.BaseOutputQuantity
                    : (decimal?)null
            })
            .ToListAsync(cancellationToken);

        var existingRuleIds = await _dbContext.MfgProductionOrderLosses.AsNoTracking()
            .Where(x => x.MfgProductionOrderId == order.MfgProductionOrderId &&
                        x.SourceManufacturingBomLossRuleId.HasValue)
            .Select(x => x.SourceManufacturingBomLossRuleId!.Value)
            .ToListAsync(cancellationToken);
        var existingSet = existingRuleIds.ToHashSet();
        var now = DateTime.Now;
        var newRows = rules.Where(x => !existingSet.Contains(x.Rule.ManufacturingBomLossRuleId))
            .Select(x => new MfgProductionOrderLoss
            {
                MfgProductionOrderLossId = Guid.CreateVersion7(),
                CompanyId = companyId,
                MfgProductionOrderId = order.MfgProductionOrderId,
                SourceManufacturingBomLossRuleId = x.Rule.ManufacturingBomLossRuleId,
                LossTypeCodeSnapshot = x.LossTypeCode,
                LossTypeNameSnapshot = x.LossTypeName,
                CalculationMethodSnapshot = x.Rule.CalculationMethod,
                StageCodeSnapshot = x.StageCode,
                MaterialCodeSnapshot = x.MaterialCode,
                RatePercentSnapshot = x.Rule.RatePercent,
                FixedQuantityKgSnapshot = x.Rule.FixedQuantityKg,
                QuantityPerEventKgSnapshot = x.Rule.QuantityPerEventKg,
                IncludeInMaterialRequestSnapshot = x.Rule.IncludeInMaterialRequest,
                PlannedQuantityKg = ProductionLossCalculator.CalculatePlannedQuantityKg(
                    x.Rule.CalculationMethod,
                    order.TotalQuantityRequest,
                    order.NumOfBatches ?? 1,
                    x.MaterialQuantityPerOutput,
                    x.StageQuantityPerOutput,
                    x.Rule.RatePercent,
                    x.Rule.FixedQuantityKg,
                    x.Rule.QuantityPerEventKg,
                    x.Rule.DefaultEventCount),
                EventCount = x.Rule.DefaultEventCount,
                RecoveredQuantityKg = 0,
                IsFinalized = false,
                Note = x.Rule.Note,
                RecordedDate = now,
                RecordedBy = employeeId
            }).ToList();

        if (newRows.Count > 0)
        {
            await _dbContext.MfgProductionOrderLosses.AddRangeAsync(newRows, cancellationToken);
            _dbContext.AuditLogs.Add(BomAudit.Create(
                companyId,
                employeeId,
                "mfg_production_order_losses",
                order.MfgProductionOrderId,
                "InitializeProductionOrderLosses",
                new { SourceBomVersionId = sourceBomVersionId.Value, LossCount = newRows.Count },
                actionType: AuditActionType.Create,
                schemaName: "manufacturing"));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var allRows = await _dbContext.MfgProductionOrderLosses.AsNoTracking()
            .Where(x => x.MfgProductionOrderId == order.MfgProductionOrderId && x.CompanyId == companyId)
            .OrderBy(x => x.RecordedDate)
            .ToListAsync(cancellationToken);
        return OperationResult<IReadOnlyList<ProductionOrderLossDto>>.Ok(
            allRows.Select(ProductionOrderLossMapper.ToDto).ToList());
    }
}
