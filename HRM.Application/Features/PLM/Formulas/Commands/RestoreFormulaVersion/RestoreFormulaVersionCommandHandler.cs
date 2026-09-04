using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.RestoreFormulaVersion;

internal sealed class RestoreFormulaVersionCommandHandler
    : IRequestHandler<RestoreFormulaVersionCommand, OperationResult<FormulaVersionActionResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly FormulaVersionService _formulaVersionService;

    public RestoreFormulaVersionCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        FormulaVersionService formulaVersionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _formulaVersionService = formulaVersionService;
    }

    public async Task<OperationResult<FormulaVersionActionResultDto>> Handle(
        RestoreFormulaVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FormulaId == Guid.Empty || command.VersionNo <= 0 ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(
                "Formula, version, company, and employee context are required.");
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

        if (FormulaConcurrencyRules.HasExpectedUpdatedDateConflict(
                command.Request.ExpectedUpdatedDate,
                formula.UpdatedDate,
                formula.CreatedDate))
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(
                "Formula was changed by another user. Please reload before restoring.");
        }

        var sourceVersion = await _dbContext.FormulaVersions
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.FormulaId == formula.FormulaId &&
                x.VersionNo == command.VersionNo,
                cancellationToken);

        if (sourceVersion is null)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(
                "Formula version was not found.");
        }

        var activeMaterials = await _dbContext.FormulaMaterials
            .Where(x => x.FormulaId == formula.FormulaId && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var material in activeMaterials)
        {
            material.IsActive = false;
        }

        formula.Name = sourceVersion.Name;
        formula.Status = sourceVersion.Status;
        formula.Note = sourceVersion.Note;
        formula.TotalPrice = sourceVersion.TotalPrice;
        formula.ProductionPrice = sourceVersion.ProductionPrice;
        formula.PresidentPrice = sourceVersion.PresidentPrice;
        formula.ProfitMarginPrice = sourceVersion.ProfitMarginPrice;
        formula.UpdatedBy = employeeId;
        formula.UpdatedDate = DateTime.Now;

        foreach (var item in sourceVersion.Items.OrderBy(x => x.LineNo))
        {
            var material = new FormulaMaterial
            {
                FormulaMaterialId = Guid.CreateVersion7(),
                FormulaId = formula.FormulaId,
                Formula = formula,
                LineNo = item.LineNo,
                itemType = item.ItemType,
                MaterialId = item.MaterialId,
                ProductId = item.ProductId,
                CategoryId = item.CategoryId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                Unit = item.Unit,
                MaterialExternalIdSnapshot = item.MaterialExternalIdSnapshot,
                MaterialNameSnapshot = item.MaterialNameSnapshot,
                IsActive = true
            };

            await _dbContext.FormulaMaterials.AddAsync(material, cancellationToken);
        }

        try
        {
            var restoredVersion = await _formulaVersionService.SaveSnapshotAsync(
                formula,
                employeeId,
                formula.UpdatedDate.Value,
                BuildChangeReason(command.VersionNo, command.Request.ChangeReason),
                force: true,
                cancellationToken);

            return OperationResult<FormulaVersionActionResultDto>.Ok(new FormulaVersionActionResultDto
            {
                FormulaId = formula.FormulaId,
                FormulaVersionId = restoredVersion!.FormulaVersionId,
                VersionNo = restoredVersion.VersionNo
            });
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(ex.Message);
        }
        catch (DbUpdateException)
        {
            return OperationResult<FormulaVersionActionResultDto>.Fail(
                "Formula version was restored concurrently. Reload and try again.");
        }
    }

    private static string BuildChangeReason(int versionNo, string? changeReason)
    {
        var suffix = string.IsNullOrWhiteSpace(changeReason)
            ? string.Empty
            : $": {changeReason.Trim()}";

        return $"Restored from version {versionNo}{suffix}";
    }
}
