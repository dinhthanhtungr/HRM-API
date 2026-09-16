using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.FinalizeProductionOrderLosses;

internal sealed class FinalizeProductionOrderLossesCommandHandler
    : IRequestHandler<FinalizeProductionOrderLossesCommand, OperationResult>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public FinalizeProductionOrderLossesCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult> Handle(
        FinalizeProductionOrderLossesCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult.Fail("Current company or employee is invalid.");

        var rows = await _dbContext.MfgProductionOrderLosses
            .Where(x => x.CompanyId == companyId && x.MfgProductionOrderId == command.MfgProductionOrderId)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return OperationResult.Fail("Production order does not have loss snapshots.");
        if (rows.Any(x => !x.IsFinalized && !x.ActualQuantityKg.HasValue))
            return OperationResult.Fail("ActualQuantityKg is required for every loss before finalization.");

        var now = DateTime.Now;
        foreach (var row in rows.Where(x => !x.IsFinalized))
        {
            row.IsFinalized = true;
            row.UpdatedDate = now;
            row.UpdatedBy = employeeId;
        }
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "mfg_production_order_losses",
            command.MfgProductionOrderId,
            "FinalizeProductionOrderLosses",
            new { LossCount = rows.Count },
            schemaName: "manufacturing"));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Production losses finalized successfully.");
    }
}
