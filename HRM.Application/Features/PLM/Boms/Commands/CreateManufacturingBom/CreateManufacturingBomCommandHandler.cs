using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingBom;

internal sealed class CreateManufacturingBomCommandHandler
    : IRequestHandler<CreateManufacturingBomCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CreateManufacturingBomCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<BomVersionDto>> Handle(
        CreateManufacturingBomCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>.Fail("Current company or employee is invalid.");
        }

        var request = command.Request;
        var validationError = BomRules.ValidateText(request.Code, 64, nameof(request.Code), true)
            ?? BomRules.ValidateText(request.Name, 200, nameof(request.Name), true)
            ?? BomRules.ValidateText(request.OutputUnit, 32, nameof(request.OutputUnit), true)
            ?? BomRules.ValidatePeriod(request.EffectiveFrom, request.EffectiveTo);
        if (validationError is not null || request.BaseOutputQuantity <= 0)
        {
            return OperationResult<BomVersionDto>.Fail(
                validationError ?? "BaseOutputQuantity must be greater than zero.");
        }
        if (!string.Equals(request.OutputUnit.Trim(), "kg", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<BomVersionDto>.Fail(
                "Manufacturing BOM output unit must be kg until unit conversion is supported.");
        }

        var source = await _dbContext.BomVersions
            .AsNoTracking()
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.EngineeringBomVersionId &&
                     x.Status == BomVersionStatus.Released &&
                     x.BomDefinition.CompanyId == companyId &&
                     x.BomDefinition.BomType == BomType.Engineering &&
                     x.BomDefinition.IsActive,
                cancellationToken);
        if (source is null)
        {
            return OperationResult<BomVersionDto>.Fail(
                "Released source Engineering BOM was not found in your company.");
        }

        var code = request.Code.Trim();
        if (await _dbContext.BomDefinitions.AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken))
        {
            return OperationResult<BomVersionDto>.Fail("BOM code already exists in your company.");
        }

        var now = DateTime.Now;
        var definition = new BomDefinition
        {
            BomDefinitionId = Guid.CreateVersion7(),
            CompanyId = companyId,
            ProductId = source.BomDefinition.ProductId,
            Code = code,
            Name = request.Name.Trim(),
            BomType = BomType.Manufacturing,
            Description = BomRules.NormalizeOptionalText(request.Description),
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
            BaseOutputQuantity = request.BaseOutputQuantity,
            OutputUnit = request.OutputUnit.Trim(),
            SourceEngineeringBomVersionId = source.BomVersionId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Note = BomRules.NormalizeOptionalText(request.Note),
            CreatedDate = now,
            CreatedBy = employeeId
        };
        var items = source.Items.OrderBy(x => x.LineNo).Select((x, index) => new BomVersionItem
        {
            BomVersionItemId = Guid.CreateVersion7(),
            BomVersionId = version.BomVersionId,
            LineNo = index + 1,
            ItemType = x.ItemType,
            MaterialId = x.MaterialId,
            ComponentProductId = x.ComponentProductId,
            CategoryId = x.CategoryId,
            Quantity = x.Quantity,
            Unit = x.Unit,
            MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot,
            MaterialNameSnapshot = x.MaterialNameSnapshot,
            Note = x.Note
        }).ToList();

        await _dbContext.BomDefinitions.AddAsync(definition, cancellationToken);
        await _dbContext.BomVersions.AddAsync(version, cancellationToken);
        await _dbContext.BomVersionItems.AddRangeAsync(items, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "bom_definitions",
            definition.BomDefinitionId,
            "CreateManufacturingBom",
            new { version.BomVersionId, SourceEngineeringBomVersionId = source.BomVersionId, ItemCount = items.Count },
            actionType: AuditActionType.Create));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<BomVersionDto>.Ok(BomMapper.ToWriteResponseDto(definition, version, items));
    }
}
