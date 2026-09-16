using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ReplaceManufacturingBom;

internal sealed class ReplaceManufacturingBomCommandHandler
    : IRequestHandler<ReplaceManufacturingBomCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ManufacturingBomStructureService _structureService;

    public ReplaceManufacturingBomCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ManufacturingBomStructureService structureService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _structureService = structureService;
    }

    public async Task<OperationResult<BomVersionDto>> Handle(
        ReplaceManufacturingBomCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>.Fail("Current company or employee is invalid.");
        }

        var version = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .Include(x => x.ManufacturingStages)
            .Include(x => x.LossRules)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.BomVersionId &&
                     x.BomDefinition.CompanyId == companyId &&
                     x.BomDefinition.BomType == BomType.Manufacturing,
                cancellationToken);
        if (version is null)
        {
            return OperationResult<BomVersionDto>.Fail("Manufacturing BOM version was not found.");
        }
        if (version.Status != BomVersionStatus.Draft)
        {
            return OperationResult<BomVersionDto>.Fail("Only Draft Manufacturing BOM versions can be changed.");
        }
        if (FormulaDrivenManufacturingBomMarker.IsFormulaDriven(version.ChangeReason))
        {
            return OperationResult<BomVersionDto>.Fail(
                "Formula-driven Manufacturing BOM items are immutable. Use the process-configuration endpoint to assign stages and loss rules.");
        }

        var structure = await _structureService.BuildAsync(
            version.BomVersionId,
            version.BomDefinition.ProductId,
            command.Request,
            companyId,
            cancellationToken);
        if (structure.Error is not null)
        {
            return OperationResult<BomVersionDto>.Fail(structure.Error);
        }

        _dbContext.ManufacturingBomLossRules.RemoveRange(version.LossRules);
        _dbContext.BomVersionItems.RemoveRange(version.Items);
        _dbContext.ManufacturingBomStages.RemoveRange(version.ManufacturingStages);

        version.BaseOutputQuantity = command.Request.BaseOutputQuantity;
        version.OutputUnit = command.Request.OutputUnit.Trim();
        version.EffectiveFrom = command.Request.EffectiveFrom;
        version.EffectiveTo = command.Request.EffectiveTo;
        version.ChangeReason = BomRules.NormalizeOptionalText(command.Request.ChangeReason);
        version.Note = BomRules.NormalizeOptionalText(command.Request.Note);
        version.BomDefinition.UpdatedBy = employeeId;
        version.BomDefinition.UpdatedDate = DateTime.Now;

        await _dbContext.ManufacturingBomStages.AddRangeAsync(structure.Stages, cancellationToken);
        await _dbContext.BomVersionItems.AddRangeAsync(structure.Items, cancellationToken);
        await _dbContext.ManufacturingBomLossRules.AddRangeAsync(structure.LossRules, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "bom_versions",
            version.BomVersionId,
            "ReplaceManufacturingBomDraft",
            new
            {
                ItemCount = structure.Items.Count,
                StageCount = structure.Stages.Count,
                LossRuleCount = structure.LossRules.Count
            },
            version.ChangeReason));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<BomVersionDto>.Ok(
            BomMapper.ToWriteResponseDto(version.BomDefinition, version, structure.Items));
    }
}
