using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaInformation;

internal sealed class UpdateFormulaInformationCommandHandler
    : IRequestHandler<UpdateFormulaInformationCommand, OperationResult<FormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly FormulaWriteService _formulaWriteService;

    public UpdateFormulaInformationCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaWriteService formulaWriteService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _formulaWriteService = formulaWriteService;
    }

    public async Task<OperationResult<FormulaWriteResultDto>> Handle(
        UpdateFormulaInformationCommand command,
        CancellationToken cancellationToken)
    {
        try
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
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x =>
                    x.FormulaId == command.FormulaId &&
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.Product.CompanyId == companyId &&
                    x.Product.IsActive,
                    cancellationToken);

            if (formula is null)
            {
                return OperationResult<FormulaWriteResultDto>.Fail("Formula was not found or is not accessible.");
            }

            if (FormulaConcurrencyRules.HasExpectedUpdatedDateConflict(
                    command.Request.ExpectedUpdatedDate,
                    formula.UpdatedDate))
            {
                return OperationResult<FormulaWriteResultDto>.Fail("Formula was changed by another user. Please reload before saving.");
            }

            if (command.Request.ProductId is { } productId && productId != Guid.Empty)
            {
                var product = await _formulaWriteService.LoadProductAsync(
                    companyId,
                    productId,
                    cancellationToken);
                formula.ProductId = product.ProductId;
            }

            formula.ExternalId = await _formulaWriteService.ResolveExternalIdAsync(
                companyId,
                command.Request.ExternalId,
                formula.FormulaId,
                cancellationToken);
            formula.Name = FormulaWriteService.NormalizeRequiredName(command.Request.Name);
            formula.Note = NormalizeOptionalText(command.Request.Note);
            formula.EffectiveDate = command.Request.EffectiveDate;
            formula.IsSelect = command.Request.IsSelect ?? formula.IsSelect;
            formula.UpdatedBy = employeeId;
            formula.UpdatedDate = DateTime.Now;

            await _formulaWriteService.ReplaceMaterialsAsync(
                formula,
                command.Request.Materials,
                companyId,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<FormulaWriteResultDto>.Ok(
                FormulaWriteService.ToResult(formula),
                "Updated formula successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(ex.Message);
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}
