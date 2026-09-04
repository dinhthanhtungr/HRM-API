using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.CloneFormula;

internal sealed class CloneFormulaCommandHandler
    : IRequestHandler<CloneFormulaCommand, OperationResult<FormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly FormulaWriteService _formulaWriteService;
    private readonly FormulaVersionService _formulaVersionService;

    public CloneFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaWriteService formulaWriteService,
        FormulaVersionService formulaVersionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _formulaWriteService = formulaWriteService;
        _formulaVersionService = formulaVersionService;
    }

    public async Task<OperationResult<FormulaWriteResultDto>> Handle(
        CloneFormulaCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourceFormulaId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("SourceFormulaId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Current company is invalid.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Current user does not have an employee profile.");
        }

        var source = await _dbContext.Formulas
            .Include(x => x.FormulaMaterials)
            .FirstOrDefaultAsync(x =>
                x.FormulaId == command.SourceFormulaId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive,
                cancellationToken);

        if (source is null)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Source formula was not found or is not accessible.");
        }

        var now = DateTime.Now;
        var clone = new Formula
        {
            FormulaId = Guid.CreateVersion7(),
            ExternalId = await _formulaWriteService.ResolveExternalIdAsync(
                companyId,
                requestedExternalId: null,
                excludedFormulaId: null,
                cancellationToken),
            Name = await _formulaWriteService.ResolveNameAsync(
                companyId,
                source.ProductId,
                requestedName: null,
                cancellationToken),
            ProductId = source.ProductId,
            Status = FormulaStatus.Draft.ToString(),
            TotalPrice = source.TotalPrice,
            EffectiveDate = source.EffectiveDate,
            ProductionPrice = source.ProductionPrice,
            PresidentPrice = source.PresidentPrice,
            ProfitMarginPrice = source.ProfitMarginPrice,
            IsSelect = false,
            IsActive = true,
            Note = source.Note,
            CreatedBy = employeeId,
            CreatedDate = now,
            UpdatedBy = employeeId,
            UpdatedDate = now,
            CompanyId = companyId
        };

        await _dbContext.Formulas.AddAsync(clone, cancellationToken);

        var sourceMaterials = source.FormulaMaterials
            .Where(x => x.IsActive)
            .OrderBy(x => x.LineNo)
            .ThenBy(x => x.FormulaMaterialId)
            .ToArray();

        for (var index = 0; index < sourceMaterials.Length; index++)
        {
            var sourceMaterial = sourceMaterials[index];
            await _dbContext.FormulaMaterials.AddAsync(new FormulaMaterial
            {
                FormulaMaterialId = Guid.CreateVersion7(),
                FormulaId = clone.FormulaId,
                Formula = clone,
                MaterialId = sourceMaterial.MaterialId,
                ProductId = sourceMaterial.ProductId,
                CategoryId = sourceMaterial.CategoryId,
                Quantity = sourceMaterial.Quantity,
                UnitPrice = sourceMaterial.UnitPrice,
                TotalPrice = sourceMaterial.TotalPrice,
                itemType = sourceMaterial.itemType,
                MaterialNameSnapshot = sourceMaterial.MaterialNameSnapshot,
                MaterialExternalIdSnapshot = sourceMaterial.MaterialExternalIdSnapshot,
                Unit = sourceMaterial.Unit,
                IsActive = true,
                LineNo = index + 1
            }, cancellationToken);
        }

        try
        {
            await _formulaVersionService.SaveSnapshotAsync(
                clone,
                employeeId,
                now,
                $"Cloned from formula {source.ExternalId}",
                force: true,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<FormulaWriteResultDto>.Fail(ex.Message);
        }
        catch (DbUpdateException)
        {
            return OperationResult<FormulaWriteResultDto>.Fail("Formula clone was changed concurrently. Please try again.");
        }

        return OperationResult<FormulaWriteResultDto>.Ok(
            FormulaWriteService.ToResult(clone),
            "Cloned formula successfully.");
    }
}
