using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.PatchProductionOrderLoss;

internal sealed class PatchProductionOrderLossCommandHandler
    : IRequestHandler<PatchProductionOrderLossCommand, OperationResult<ProductionOrderLossDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PatchProductionOrderLossCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ProductionOrderLossDto>> Handle(
        PatchProductionOrderLossCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ProductionOrderLossDto>.Fail("Current company or employee is invalid.");
        }
        var row = await _dbContext.MfgProductionOrderLosses.FirstOrDefaultAsync(
            x => x.MfgProductionOrderLossId == command.LossId &&
                 x.MfgProductionOrderId == command.MfgProductionOrderId &&
                 x.CompanyId == companyId,
            cancellationToken);
        if (row is null) return OperationResult<ProductionOrderLossDto>.Fail("Production loss was not found.");
        if (row.IsFinalized) return OperationResult<ProductionOrderLossDto>.Fail("Finalized production loss cannot be changed.");

        var request = command.Request;
        if (request.ActualQuantityKg is < 0 || request.RecoveredQuantityKg is < 0 || request.EventCount is < 0)
            return OperationResult<ProductionOrderLossDto>.Fail("Loss quantities and EventCount cannot be negative.");
        if (request.ActualQuantityKg.HasValue) row.ActualQuantityKg = request.ActualQuantityKg;
        if (request.RecoveredQuantityKg.HasValue) row.RecoveredQuantityKg = request.RecoveredQuantityKg.Value;
        if (request.EventCount.HasValue)
        {
            row.EventCount = request.EventCount;
            if (row.CalculationMethodSnapshot == LossCalculationMethod.FixedPerEvent)
            {
                row.PlannedQuantityKg = decimal.Round(
                    (row.QuantityPerEventKgSnapshot ?? 0m) * request.EventCount.Value,
                    3,
                    MidpointRounding.AwayFromZero);
            }
        }
        if (request.Note is not null) row.Note = BomRules.NormalizeOptionalText(request.Note);
        row.UpdatedDate = DateTime.Now;
        row.UpdatedBy = employeeId;
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "mfg_production_order_losses",
            row.MfgProductionOrderLossId,
            "PatchProductionOrderLoss",
            new { row.ActualQuantityKg, row.EventCount, row.RecoveredQuantityKg },
            schemaName: "manufacturing"));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ProductionOrderLossDto>.Ok(ProductionOrderLossMapper.ToDto(row));
    }
}
