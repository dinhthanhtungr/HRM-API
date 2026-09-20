using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingBomFromSelectedFormula;

internal sealed class CreateManufacturingBomFromSelectedFormulaCommandHandler
    : IRequestHandler<CreateManufacturingBomFromSelectedFormulaCommand, OperationResult<FormulaDrivenManufacturingBomDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly BomItemResolver _itemResolver;

    public CreateManufacturingBomFromSelectedFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        BomItemResolver itemResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _itemResolver = itemResolver;
    }

    public async Task<OperationResult<FormulaDrivenManufacturingBomDto>> Handle(
        CreateManufacturingBomFromSelectedFormulaCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ProductId == Guid.Empty)
        {
            return OperationResult<FormulaDrivenManufacturingBomDto>.Fail("ProductId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<FormulaDrivenManufacturingBomDto>.Fail("Current company or employee is invalid.");
        }

        var formula = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == command.ProductId &&
                x.IsActive &&
                x.IsSelect &&
                x.Status == FormulaStatus.Completed.ToString() &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId)
            .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new SelectedFormula(x.FormulaId, x.ExternalId, x.Name))
            .FirstOrDefaultAsync(cancellationToken);
        if (formula is null)
        {
            return OperationResult<FormulaDrivenManufacturingBomDto>.Fail(
                "No active customer-selected completed Formula was found for this Product.");
        }

        var formulaMarker = FormulaDrivenManufacturingBomMarker.Create(formula.FormulaId);
        var existing = await _dbContext.BomVersions
            .AsNoTracking()
            .Where(x =>
                x.ChangeReason == formulaMarker &&
                x.BomDefinition.CompanyId == companyId &&
                x.BomDefinition.ProductId == command.ProductId &&
                x.BomDefinition.BomType == BomType.Manufacturing)
            .Select(x => new ExistingSnapshot(
                x.BomDefinitionId,
                x.BomVersionId,
                x.VersionNo,
                x.Status,
                x.Items.Count))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return OperationResult<FormulaDrivenManufacturingBomDto>.Ok(new FormulaDrivenManufacturingBomDto
            {
                FormulaId = formula.FormulaId,
                FormulaExternalId = formula.ExternalId,
                BomDefinitionId = existing.BomDefinitionId,
                BomVersionId = existing.BomVersionId,
                VersionNo = existing.VersionNo,
                Status = existing.Status,
                ItemCount = existing.ItemCount,
                IsExistingSnapshot = true
            });
        }

        var sourceItems = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == formula.FormulaId &&
                x.IsActive &&
                (x.itemType == ItemType.Material || x.itemType == ItemType.Product))
            .OrderBy(x => x.LineNo)
            .Select(x => new SourceFormulaItem(
                x.itemType,
                x.MaterialId ?? x.ProductId ?? Guid.Empty,
                x.Quantity,
                x.Unit,
                null))
            .ToListAsync(cancellationToken);

        var sourceItemError = ValidateSourceItems(sourceItems);
        if (sourceItemError is not null)
        {
            return OperationResult<FormulaDrivenManufacturingBomDto>.Fail(sourceItemError);
        }

        var resolution = await _itemResolver.ResolveAsync(
            command.ProductId,
            sourceItems.Select(x => new BomItemWriteDto
            {
                ItemType = x.ItemType,
                ItemId = x.ItemId,
                Quantity = x.Quantity,
                Unit = x.Unit!,
                Note = x.Note
            }).ToList(),
            companyId,
            cancellationToken);
        if (resolution.Error is not null)
        {
            return OperationResult<FormulaDrivenManufacturingBomDto>.Fail(resolution.Error);
        }

        var formulaDrivenDefinition = await _dbContext.BomDefinitions
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == command.ProductId &&
                x.BomType == BomType.Manufacturing &&
                x.IsActive &&
                x.Description != null &&
                x.Description.StartsWith("Formula-driven manufacturing BOM"))
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.Now;
        var definition = formulaDrivenDefinition;
        if (definition is null)
        {
            definition = new BomDefinition
            {
                BomDefinitionId = Guid.CreateVersion7(),
                CompanyId = companyId,
                ProductId = command.ProductId,
                ExternalId = BuildBomCode(formula),
                Name = BuildBomName(formula),
                BomType = BomType.Manufacturing,
                Description = $"Formula-driven manufacturing BOM initialized from Formula {formula.ExternalId}.",
                IsActive = true,
                CreatedDate = now,
                CreatedBy = employeeId
            };
            await _dbContext.BomDefinitions.AddAsync(definition, cancellationToken);
        }

        var nextVersionNo = definition.Versions.Count > 0
            ? definition.Versions.Max(x => x.VersionNo) + 1
            : await _dbContext.BomVersions
                .Where(x => x.BomDefinitionId == definition.BomDefinitionId)
                .Select(x => (int?)x.VersionNo)
                .MaxAsync(cancellationToken) + 1 ?? 1;

        var version = new BomVersion
        {
            BomVersionId = Guid.CreateVersion7(),
            BomDefinitionId = definition.BomDefinitionId,
            VersionNo = nextVersionNo,
            Status = BomVersionStatus.Draft,
            BaseOutputQuantity = 1m,
            OutputUnit = "kg",
            ChangeReason = formulaMarker,
            CreatedDate = now,
            CreatedBy = employeeId
        };
        var items = BomMapper.CreateVersionItems(version.BomVersionId, resolution.Items);
        await _dbContext.BomVersions.AddAsync(version, cancellationToken);
        await _dbContext.BomVersionItems.AddRangeAsync(items, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "bom_definitions",
            definition.BomDefinitionId,
            "CreateManufacturingBomFromSelectedFormula",
            new
            {
                formula.FormulaId,
                formula.ExternalId,
                version.BomVersionId,
                version.VersionNo,
                ItemCount = items.Count
            },
            actionType: AuditActionType.Create));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<FormulaDrivenManufacturingBomDto>.Ok(new FormulaDrivenManufacturingBomDto
        {
            FormulaId = formula.FormulaId,
            FormulaExternalId = formula.ExternalId,
            BomDefinitionId = definition.BomDefinitionId,
            BomVersionId = version.BomVersionId,
            VersionNo = version.VersionNo,
            Status = version.Status,
            ItemCount = items.Count,
            IsExistingSnapshot = false
        });
    }

    private static string? ValidateSourceItems(IReadOnlyList<SourceFormulaItem> sourceItems)
    {
        if (sourceItems.Count == 0)
        {
            return "The selected Formula does not contain active Material or Product items for a Manufacturing BOM.";
        }

        if (sourceItems.Any(x =>
                x.ItemId == Guid.Empty ||
                x.Quantity <= 0 ||
                !string.Equals(x.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)))
        {
            return "Selected Formula must contain valid Material or Product items with positive quantities in kg.";
        }

        return null;
    }

    private static string BuildBomCode(SelectedFormula formula)
    {
        var sourceCode = formula.ExternalId?.Trim() ?? string.Empty;
        var suffix = formula.FormulaId.ToString("N")[..8];
        var maximumSourceLength = 64 - "MBOM-".Length - suffix.Length - 1;
        if (sourceCode.Length > maximumSourceLength)
        {
            sourceCode = sourceCode[..maximumSourceLength];
        }

        return string.IsNullOrEmpty(sourceCode)
            ? $"MBOM-{suffix}"
            : $"MBOM-{sourceCode}-{suffix}";
    }

    private static string BuildBomName(SelectedFormula formula)
    {
        var sourceName = string.IsNullOrWhiteSpace(formula.Name)
            ? formula.ExternalId
            : formula.Name.Trim();
        var name = $"M-BOM - {sourceName}";
        return name.Length <= 200 ? name : name[..200];
    }

    private sealed record SelectedFormula(Guid FormulaId, string ExternalId, string Name);
    private sealed record SourceFormulaItem(ItemType ItemType, Guid ItemId, decimal Quantity, string? Unit, string? Note);
    private sealed record ExistingSnapshot(Guid BomDefinitionId, Guid BomVersionId, int VersionNo, BomVersionStatus Status, int ItemCount);
}
