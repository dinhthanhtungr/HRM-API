using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.GenerateManufacturingFormulaFromBom;

internal sealed class GenerateManufacturingFormulaFromBomCommandHandler
    : IRequestHandler<GenerateManufacturingFormulaFromBomCommand, OperationResult<GeneratedManufacturingFormulaDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GenerateManufacturingFormulaFromBomCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<GeneratedManufacturingFormulaDto>> Handle(
        GenerateManufacturingFormulaFromBomCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<GeneratedManufacturingFormulaDto>.Fail("Current company or employee is invalid.");
        }

        var request = command.Request;
        var validationError = BomRules.ValidateText(request.ExternalId, 100, nameof(request.ExternalId), true)
            ?? BomRules.ValidateText(request.Name, 200, nameof(request.Name), true);
        if (validationError is not null)
        {
            return OperationResult<GeneratedManufacturingFormulaDto>.Fail(validationError);
        }

        var version = await _dbContext.BomVersions
            .AsNoTracking()
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.BomVersionId &&
                     x.Status == BomVersionStatus.Released &&
                     x.BomDefinition.BomType == BomType.Manufacturing &&
                     x.BomDefinition.CompanyId == companyId &&
                     x.BomDefinition.IsActive,
                cancellationToken);
        if (version is null)
        {
            return OperationResult<GeneratedManufacturingFormulaDto>.Fail(
                "Released Manufacturing BOM was not found in your company.");
        }

        var externalId = request.ExternalId.Trim();
        if (await _dbContext.ManufacturingFormulas.AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId && x.ExternalId == externalId, cancellationToken))
        {
            return OperationResult<GeneratedManufacturingFormulaDto>.Fail(
                "Manufacturing formula ExternalId already exists in your company.");
        }

        var now = DateTime.Now;
        var formula = new ManufacturingFormula
        {
            ManufacturingFormulaId = Guid.CreateVersion7(),
            ExternalId = externalId,
            Name = request.Name.Trim(),
            Status = ManufacturingProductOrderFormula.New.ToString(),
            SourceBomVersionId = version.BomVersionId,
            SourceType = FormulaSource.FromBom,
            IsActive = true,
            Note = BomRules.NormalizeOptionalText(request.Note),
            CreatedDate = now,
            CreatedBy = employeeId,
            UpdatedDate = now,
            UpdatedBy = employeeId,
            CompanyId = companyId
        };

        var materials = version.Items.OrderBy(x => x.LineNo).Select(x => new ManufacturingFormulaMaterial
        {
            ManufacturingFormulaMaterialId = Guid.CreateVersion7(),
            ManufacturingFormulaId = formula.ManufacturingFormulaId,
            MaterialId = x.MaterialId,
            ProductId = x.ComponentProductId,
            CategoryId = x.CategoryId ?? Guid.Empty,
            Quantity = x.Quantity / version.BaseOutputQuantity,
            UnitPrice = 0,
            TotalPrice = 0,
            itemType = x.ItemType,
            MaterialNameSnapshot = x.MaterialNameSnapshot,
            MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot,
            Unit = x.Unit,
            IsActive = true,
            LineNo = x.LineNo
        }).ToList();
        if (materials.Any(x => x.CategoryId == Guid.Empty))
        {
            return OperationResult<GeneratedManufacturingFormulaDto>.Fail(
                "Every BOM item requires CategoryId before generating a manufacturing formula.");
        }

        await _dbContext.ManufacturingFormulas.AddAsync(formula, cancellationToken);
        await _dbContext.ManufacturingFormulaMaterials.AddRangeAsync(materials, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "bom_versions", version.BomVersionId, "generate-manufacturing-formula",
            new { formula.ManufacturingFormulaId, formula.ExternalId }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<GeneratedManufacturingFormulaDto>.Ok(new GeneratedManufacturingFormulaDto
        {
            ManufacturingFormulaId = formula.ManufacturingFormulaId,
            ExternalId = formula.ExternalId,
            SourceBomVersionId = version.BomVersionId,
            MaterialCount = materials.Count
        });
    }
}
