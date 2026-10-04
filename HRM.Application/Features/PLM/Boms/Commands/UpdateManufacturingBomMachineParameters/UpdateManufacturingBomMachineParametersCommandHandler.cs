using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.UpdateManufacturingBomMachineParameters;

internal sealed class UpdateManufacturingBomMachineParametersCommandHandler
    : IRequestHandler<
        UpdateManufacturingBomMachineParametersCommand,
        OperationResult<IReadOnlyList<ManufacturingBomStageMachineParameterDto>>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public UpdateManufacturingBomMachineParametersCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<IReadOnlyList<ManufacturingBomStageMachineParameterDto>>> Handle(
        UpdateManufacturingBomMachineParametersCommand command,
        CancellationToken cancellationToken)
    {
        if (command.BomVersionId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return Fail("Current company, employee, or BOM version is invalid.");
        }

        var version = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .Include(x => x.ManufacturingStages)
                .ThenInclude(x => x.Machines)
                .ThenInclude(x => x.Parameters)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.BomVersionId &&
                     x.BomDefinition.CompanyId == companyId &&
                     x.BomDefinition.BomType == BomType.Manufacturing,
                cancellationToken);

        if (version is null)
        {
            return Fail("Manufacturing BOM version was not found.");
        }
        if (version.Status != BomVersionStatus.Draft)
        {
            return Fail("Only Draft Manufacturing BOM versions can change machine parameter values.");
        }

        var parameters = version.ManufacturingStages
            .SelectMany(stage => stage.Machines)
            .SelectMany(machine => machine.Parameters)
            .ToList();
        var requested = command.Request.Parameters;

        if (requested.Any(x => x.ManufacturingBomStageMachineParameterId == Guid.Empty) ||
            requested.GroupBy(x => x.ManufacturingBomStageMachineParameterId).Any(group => group.Count() > 1))
        {
            return Fail("Parameters must contain unique, non-empty ManufacturingBomStageMachineParameterId values.");
        }

        var parameterById = parameters.ToDictionary(x => x.ManufacturingBomStageMachineParameterId);
        var requestedIds = requested
            .Select(x => x.ManufacturingBomStageMachineParameterId)
            .ToHashSet();
        if (requestedIds.Count != parameterById.Count ||
            requestedIds.Except(parameterById.Keys).Any())
        {
            return Fail("Parameters must contain exactly one value entry for every machine parameter in this Manufacturing BOM version.");
        }

        foreach (var source in requested)
        {
            var rangeError = ValidateRange(source);
            if (rangeError is not null)
            {
                return Fail(rangeError);
            }

            var parameter = parameterById[source.ManufacturingBomStageMachineParameterId];
            parameter.TargetValueSnapshot = source.TargetValue;
            parameter.MinValueSnapshot = source.MinValue;
            parameter.MaxValueSnapshot = source.MaxValue;
            parameter.NoteSnapshot = Normalize(source.Note);
        }

        version.BomDefinition.UpdatedBy = employeeId;
        version.BomDefinition.UpdatedDate = DateTime.Now;
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "bom_versions",
            version.BomVersionId,
            "UpdateManufacturingBomMachineParameters",
            new { ParameterCount = parameters.Count }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<IReadOnlyList<ManufacturingBomStageMachineParameterDto>>.Ok(
            parameters
                .OrderBy(x => x.StageMachine.ManufacturingStage.SequenceNo)
                .ThenBy(x => x.StageMachine.SequenceNo)
                .ThenBy(x => x.SequenceNo)
                .Select(ToDto)
                .ToList());
    }

    private static string? ValidateRange(ManufacturingBomMachineParameterValueWriteDto parameter)
    {
        if (parameter.MinValue.HasValue && parameter.MaxValue.HasValue &&
            parameter.MinValue > parameter.MaxValue)
        {
            return $"Parameter {parameter.ManufacturingBomStageMachineParameterId} has MinValue greater than MaxValue.";
        }
        if (parameter.TargetValue.HasValue && parameter.MinValue.HasValue &&
            parameter.TargetValue < parameter.MinValue)
        {
            return $"Parameter {parameter.ManufacturingBomStageMachineParameterId} has TargetValue below MinValue.";
        }
        if (parameter.TargetValue.HasValue && parameter.MaxValue.HasValue &&
            parameter.TargetValue > parameter.MaxValue)
        {
            return $"Parameter {parameter.ManufacturingBomStageMachineParameterId} has TargetValue above MaxValue.";
        }

        return null;
    }

    private static ManufacturingBomStageMachineParameterDto ToDto(
        ManufacturingBomStageMachineParameter parameter) => new()
    {
        ManufacturingBomStageMachineParameterId = parameter.ManufacturingBomStageMachineParameterId,
        ParameterCode = parameter.ParameterCodeSnapshot,
        ParameterName = parameter.ParameterNameSnapshot,
        TargetValue = parameter.TargetValueSnapshot,
        MinValue = parameter.MinValueSnapshot,
        MaxValue = parameter.MaxValueSnapshot,
        Unit = parameter.UnitSnapshot,
        IsRequired = parameter.IsRequiredSnapshot,
        SequenceNo = parameter.SequenceNo,
        Note = parameter.NoteSnapshot
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OperationResult<IReadOnlyList<ManufacturingBomStageMachineParameterDto>> Fail(
        string message) =>
        OperationResult<IReadOnlyList<ManufacturingBomStageMachineParameterDto>>.Fail(message);
}
