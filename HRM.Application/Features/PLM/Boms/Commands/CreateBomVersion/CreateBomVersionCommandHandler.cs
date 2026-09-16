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

namespace HRM.Application.Features.PLM.Boms.Commands.CreateBomVersion;

internal sealed class CreateBomVersionCommandHandler
    : IRequestHandler<CreateBomVersionCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CreateBomVersionCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<BomVersionDto>> Handle(
        CreateBomVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>.Fail("Current company or employee is invalid.");
        }

        var source = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .Include(x => x.ManufacturingStages)
            .Include(x => x.LossRules)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.Request.SourceBomVersionId &&
                     x.BomDefinitionId == command.BomDefinitionId &&
                     x.BomDefinition.CompanyId == companyId,
                cancellationToken);

        if (source is null)
        {
            return OperationResult<BomVersionDto>.Fail("Source BOM version was not found.");
        }

        if (source.Status != BomVersionStatus.Released)
        {
            return OperationResult<BomVersionDto>.Fail("A new version can only be cloned from a Released version.");
        }

        var outputUnit = command.Request.OutputUnit ?? source.OutputUnit;
        var validationError = BomRules.ValidateText(outputUnit, 32, nameof(command.Request.OutputUnit), true)
            ?? BomRules.ValidatePeriod(command.Request.EffectiveFrom, command.Request.EffectiveTo);
        if (validationError is not null || command.Request.BaseOutputQuantity is <= 0)
        {
            return OperationResult<BomVersionDto>.Fail(
                validationError ?? "BaseOutputQuantity must be greater than zero.");
        }

        var nextVersionNo = await _dbContext.BomVersions
            .Where(x => x.BomDefinitionId == command.BomDefinitionId)
            .MaxAsync(x => x.VersionNo, cancellationToken) + 1;

        var version = new BomVersion
        {
            BomVersionId = Guid.CreateVersion7(),
            BomDefinitionId = source.BomDefinitionId,
            VersionNo = nextVersionNo,
            Status = BomVersionStatus.Draft,
            BaseOutputQuantity = command.Request.BaseOutputQuantity ?? source.BaseOutputQuantity,
            OutputUnit = outputUnit.Trim(),
            SourceEngineeringBomVersionId = source.SourceEngineeringBomVersionId,
            EffectiveFrom = command.Request.EffectiveFrom,
            EffectiveTo = command.Request.EffectiveTo,
            ChangeReason = BomRules.NormalizeOptionalText(command.Request.ChangeReason),
            Note = BomRules.NormalizeOptionalText(command.Request.Note),
            CreatedDate = DateTime.Now,
            CreatedBy = employeeId
        };

        var stageMap = source.ManufacturingStages.ToDictionary(x => x.ManufacturingBomStageId, _ => Guid.CreateVersion7());
        var itemMap = source.Items.ToDictionary(x => x.BomVersionItemId, _ => Guid.CreateVersion7());
        var stages = source.ManufacturingStages.Select(x => new ManufacturingBomStage
        {
            ManufacturingBomStageId = stageMap[x.ManufacturingBomStageId],
            BomVersionId = version.BomVersionId,
            Code = x.Code,
            Name = x.Name,
            SequenceNo = x.SequenceNo,
            Description = x.Description,
            IsActive = x.IsActive
        }).ToList();
        var items = source.Items.Select(x => new BomVersionItem
        {
            BomVersionItemId = itemMap[x.BomVersionItemId],
            BomVersionId = version.BomVersionId,
            LineNo = x.LineNo,
            ItemType = x.ItemType,
            MaterialId = x.MaterialId,
            ComponentProductId = x.ComponentProductId,
            CategoryId = x.CategoryId,
            ManufacturingBomStageId = x.ManufacturingBomStageId.HasValue ? stageMap[x.ManufacturingBomStageId.Value] : null,
            Quantity = x.Quantity,
            Unit = x.Unit,
            MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot,
            MaterialNameSnapshot = x.MaterialNameSnapshot,
            Note = x.Note
        }).ToList();
        var rules = source.LossRules.Select(x => new ManufacturingBomLossRule
        {
            ManufacturingBomLossRuleId = Guid.CreateVersion7(),
            BomVersionId = version.BomVersionId,
            ManufacturingLossTypeId = x.ManufacturingLossTypeId,
            BomVersionItemId = x.BomVersionItemId.HasValue ? itemMap[x.BomVersionItemId.Value] : null,
            ManufacturingBomStageId = x.ManufacturingBomStageId.HasValue ? stageMap[x.ManufacturingBomStageId.Value] : null,
            CalculationMethod = x.CalculationMethod,
            RatePercent = x.RatePercent,
            FixedQuantityKg = x.FixedQuantityKg,
            QuantityPerEventKg = x.QuantityPerEventKg,
            DefaultEventCount = x.DefaultEventCount,
            SequenceNo = x.SequenceNo,
            IsRecoverable = x.IsRecoverable,
            IncludeInMaterialRequest = x.IncludeInMaterialRequest,
            IsActive = x.IsActive,
            Note = x.Note
        }).ToList();

        await _dbContext.BomVersions.AddAsync(version, cancellationToken);
        await _dbContext.ManufacturingBomStages.AddRangeAsync(stages, cancellationToken);
        await _dbContext.BomVersionItems.AddRangeAsync(items, cancellationToken);
        await _dbContext.ManufacturingBomLossRules.AddRangeAsync(rules, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "bom_versions",
            version.BomVersionId,
            "CreateBomVersion",
            new { SourceBomVersionId = source.BomVersionId, version.VersionNo },
            command.Request.ChangeReason,
            AuditActionType.Create));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<BomVersionDto>.Ok(BomMapper.ToWriteResponseDto(source.BomDefinition, version, items));
    }
}
