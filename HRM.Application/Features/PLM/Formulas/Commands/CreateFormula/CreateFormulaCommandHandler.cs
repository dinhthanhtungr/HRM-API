using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.CreateFormula;

internal sealed class CreateFormulaCommandHandler
    : IRequestHandler<CreateFormulaCommand, OperationResult<FormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly FormulaWriteService _formulaWriteService;

    public CreateFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaWriteService formulaWriteService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _formulaWriteService = formulaWriteService;
    }

    public async Task<OperationResult<FormulaWriteResultDto>> Handle(
        CreateFormulaCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            {
                return OperationResult<FormulaWriteResultDto>.Fail("Current company is invalid.");
            }

            if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            {
                return OperationResult<FormulaWriteResultDto>.Fail("Current user does not have an employee profile.");
            }

            var request = command.Request;
            if (!request.ProductId.HasValue || request.ProductId.Value == Guid.Empty)
            {
                return OperationResult<FormulaWriteResultDto>.Fail("ProductId is required.");
            }

            var product = await _formulaWriteService.LoadProductAsync(
                companyId,
                request.ProductId.Value,
                cancellationToken);

            var now = DateTime.Now;
            var formula = new Formula
            {
                FormulaId = Guid.CreateVersion7(),
                ExternalId = await _formulaWriteService.ResolveExternalIdAsync(
                    companyId,
                    request.ExternalId,
                    excludedFormulaId: null,
                    cancellationToken),
                Name = await _formulaWriteService.ResolveNameAsync(
                    companyId,
                    product.ProductId,
                    request.Name,
                    cancellationToken),
                ProductId = product.ProductId,
                Status = FormulaStatus.Draft.ToString(),
                TotalPrice = 0m,
                EffectiveDate = request.EffectiveDate,
                IsSelect = request.IsSelect ?? false,
                IsActive = true,
                Note = NormalizeOptionalText(request.Note),
                CreatedBy = employeeId,
                CreatedDate = now,
                UpdatedBy = employeeId,
                UpdatedDate = now,
                CompanyId = companyId
            };

            await _dbContext.Formulas.AddAsync(formula, cancellationToken);
            await _formulaWriteService.ReplaceMaterialsAsync(
                formula,
                request.Materials,
                companyId,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<FormulaWriteResultDto>.Ok(
                FormulaWriteService.ToResult(formula),
                "Created formula successfully.");
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
