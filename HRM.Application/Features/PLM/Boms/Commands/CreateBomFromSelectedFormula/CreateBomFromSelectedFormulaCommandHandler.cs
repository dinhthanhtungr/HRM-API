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

namespace HRM.Application.Features.PLM.Boms.Commands.CreateBomFromSelectedFormula;

internal sealed class CreateBomFromSelectedFormulaCommandHandler
    : IRequestHandler<CreateBomFromSelectedFormulaCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly BomItemResolver _itemResolver;

    public CreateBomFromSelectedFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        BomItemResolver itemResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _itemResolver = itemResolver;
    }

    public async Task<OperationResult<BomVersionDto>> Handle(
        CreateBomFromSelectedFormulaCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ProductId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>.Fail("ProductId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>.Fail("Current company or employee is invalid.");
        }

        var existingBom = await _dbContext.BomDefinitions
            .AsNoTracking()
            .AnyAsync(
                x => x.CompanyId == companyId &&
                     x.ProductId == command.ProductId &&
                     x.BomType == BomType.Engineering,
                cancellationToken);
        if (existingBom)
        {
            return OperationResult<BomVersionDto>.Fail(
                "This Product already has an Engineering BOM. Create a new version instead.");
        }

        var formula = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.ProductId == command.ProductId &&
                x.CompanyId == companyId &&
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
            return OperationResult<BomVersionDto>.Fail(
                "No active customer-selected completed Formula was found for this Product.");
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
            return OperationResult<BomVersionDto>.Fail(sourceItemError);
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
            return OperationResult<BomVersionDto>.Fail(resolution.Error);
        }

        var now = DateTime.Now;
        var definition = new BomDefinition
        {
            BomDefinitionId = Guid.CreateVersion7(),
            CompanyId = companyId,
            ProductId = command.ProductId,
            Code = BuildBomCode(formula),
            Name = BuildBomName(formula),
            BomType = BomType.Engineering,
            Description = $"Initialized from customer-selected Formula {formula.ExternalId}.",
            IsActive = true,
            CreatedDate = now,
            CreatedBy = employeeId
        };
        var version = new BomVersion
        {
            BomVersionId = Guid.CreateVersion7(),
            BomDefinitionId = definition.BomDefinitionId,
            VersionNo = 1,
            Status = BomVersionStatus.Draft,
            BaseOutputQuantity = 1m,
            OutputUnit = "kg",
            ChangeReason = $"Initialized from customer-selected Formula {formula.ExternalId} ({formula.FormulaId}).",
            CreatedDate = now,
            CreatedBy = employeeId
        };
        var items = BomMapper.CreateVersionItems(version.BomVersionId, resolution.Items);

        await _dbContext.BomDefinitions.AddAsync(definition, cancellationToken);
        await _dbContext.BomVersions.AddAsync(version, cancellationToken);
        await _dbContext.BomVersionItems.AddRangeAsync(items, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "bom_definitions",
            definition.BomDefinitionId,
            "CreateEngineeringBomFromSelectedFormula",
            new { formula.FormulaId, formula.ExternalId, version.BomVersionId, ItemCount = items.Count },
            actionType: AuditActionType.Create));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<BomVersionDto>.Ok(
            BomMapper.ToWriteResponseDto(definition, version, items));
    }

    private static string? ValidateSourceItems(IReadOnlyList<SourceFormulaItem> sourceItems)
    {
        if (sourceItems.Count == 0)
        {
            return "The selected Formula does not contain active Material or Product items for an Engineering BOM.";
        }

        if (sourceItems.Any(x =>
                x.ItemId == Guid.Empty ||
                x.Quantity <= 0 ||
                string.IsNullOrWhiteSpace(x.Unit) ||
                x.Unit.Trim().Length > 32))
        {
            return "Selected Formula contains an invalid BOM item. Each item requires a source, positive quantity, and Unit up to 32 characters.";
        }

        return null;
    }

    private static string BuildBomCode(SelectedFormula formula)
    {
        var sourceCode = formula.ExternalId?.Trim() ?? string.Empty;
        var suffix = formula.FormulaId.ToString("N")[..8];
        var maximumSourceLength = 64 - "EBOM-".Length - suffix.Length - 1;
        if (sourceCode.Length > maximumSourceLength)
        {
            sourceCode = sourceCode[..maximumSourceLength];
        }

        return string.IsNullOrEmpty(sourceCode)
            ? $"EBOM-{suffix}"
            : $"EBOM-{sourceCode}-{suffix}";
    }

    private static string BuildBomName(SelectedFormula formula)
    {
        var sourceName = string.IsNullOrWhiteSpace(formula.Name)
            ? formula.ExternalId
            : formula.Name.Trim();
        var name = $"E-BOM - {sourceName}";
        return name.Length <= 200 ? name : name[..200];
    }

    private sealed record SelectedFormula(Guid FormulaId, string ExternalId, string Name);

    private sealed record SourceFormulaItem(
        ItemType ItemType,
        Guid ItemId,
        decimal Quantity,
        string? Unit,
        string? Note);
}
