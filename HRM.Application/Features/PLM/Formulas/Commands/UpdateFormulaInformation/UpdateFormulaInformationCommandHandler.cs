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
    private readonly FormulaVersionService _formulaVersionService;
    private readonly FormulaPricingReviewService _pricingReview;

    public UpdateFormulaInformationCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaWriteService formulaWriteService,
        FormulaVersionService formulaVersionService,
        FormulaPricingReviewService pricingReview)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _formulaWriteService = formulaWriteService;
        _formulaVersionService = formulaVersionService;
        _pricingReview = pricingReview;
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
                    formula.UpdatedDate,
                    formula.CreatedDate))
            {
                return OperationResult<FormulaWriteResultDto>.Fail("Formula was changed by another user. Please reload before saving.");
            }

            var now = DateTime.Now;
            var materialsChanged = await _pricingReview.RequiresMaterialReviewAsync(
                formula, command.Request, companyId, cancellationToken);
            await _formulaWriteService.ApplyFormulaUpdateAsync(
                formula,
                command.Request,
                companyId,
                employeeId,
                now,
                cancellationToken);

            if (materialsChanged)
            {
                formula.CheckBy = employeeId;
                formula.CheckDate = now;
            }

            await _formulaVersionService.SaveSnapshotAsync(
                formula,
                employeeId,
                now,
                "Updated formula information",
                force: false,
                cancellationToken);

            if (materialsChanged)
            {
                var sendResult = await _pricingReview.SendMaterialChangeAsync(
                    formula, companyId, sampleRequestId: null, cancellationToken);
                if (!sendResult.Success)
                {
                    return OperationResult<FormulaWriteResultDto>.Ok(
                        FormulaWriteService.ToResult(formula),
                        $"Updated formula successfully, but could not send material-change notification: {sendResult.Message}");
                }
            }

            return OperationResult<FormulaWriteResultDto>.Ok(
                FormulaWriteService.ToResult(formula),
                "Updated formula successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(ex.Message);
        }
        catch (DbUpdateException)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(
                "Formula version was created concurrently. Reload and try again.");
        }
    }

}
