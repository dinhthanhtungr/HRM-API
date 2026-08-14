using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using HRM.Application.Features.PLM.Formulas.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.SaveFormulaVersion;

internal sealed class SaveFormulaVersionCommandHandler
    : IRequestHandler<SaveFormulaVersionCommand, OperationResult<FormulaVersionActionResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly FormulaVersionService _formulaVersionService;

    public SaveFormulaVersionCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaVersionService formulaVersionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _formulaVersionService = formulaVersionService;
    }

    public async Task<OperationResult<FormulaVersionActionResultDto>> Handle(
        SaveFormulaVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FormulaId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(
                "Formula, company, and employee context are required.");
        }

        var formula = await _dbContext.Formulas
            .FirstOrDefaultAsync(x =>
                x.FormulaId == command.FormulaId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);

        if (formula is null)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(
                "Formula was not found or is not accessible.");
        }

        try
        {
            var version = await _formulaVersionService.SaveSnapshotAsync(
                formula,
                employeeId,
                DateTime.Now,
                command.Request.ChangeReason ?? "Saved formula version",
                force: true,
                cancellationToken);

            return OperationResult<FormulaVersionActionResultDto>.Ok(new FormulaVersionActionResultDto
            {
                FormulaId = formula.FormulaId,
                FormulaVersionId = version!.FormulaVersionId,
                VersionNo = version.VersionNo
            });
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(ex.Message);
        }
        catch (DbUpdateException)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(
                "Formula version was created concurrently. Reload and try again.");
        }
    }
}
