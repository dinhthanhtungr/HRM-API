using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.DeleteFormula;

internal sealed class DeleteFormulaCommandHandler
    : IRequestHandler<DeleteFormulaCommand, OperationResult<FormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public DeleteFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<FormulaWriteResultDto>> Handle(
        DeleteFormulaCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FormulaId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("FormulaId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Current company is invalid.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Current user does not have an employee profile.");
        }

        var formula = await _dbContext.Formulas
            .FirstOrDefaultAsync(x =>
                x.FormulaId == command.FormulaId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);

        if (formula is null)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Formula was not found or is already deleted.");
        }

        var materials = await _dbContext.FormulaMaterials
            .Where(x => x.FormulaId == formula.FormulaId && x.IsActive)
            .ToListAsync(cancellationToken);

        formula.IsActive = false;
        formula.IsSelect = false;
        formula.UpdatedBy = employeeId;
        formula.UpdatedDate = DateTime.Now;

        foreach (var material in materials)
        {
            material.IsActive = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<FormulaWriteResultDto>.Ok(
            FormulaWriteService.ToResult(formula),
            "Deleted formula successfully.");
    }
}
