using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.PatchBomVersion;

internal sealed class PatchBomVersionCommandHandler
    : IRequestHandler<PatchBomVersionCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PatchBomVersionCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<BomVersionDto>> Handle(
        PatchBomVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>
                .Fail("Current company or employee is invalid.");
        }

        var version = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.BomVersionId &&
                     x.BomDefinition.CompanyId == companyId &&
                     x.BomDefinition.BomType == BomType.Engineering,
                cancellationToken);

        if (version is null)
        {
            return OperationResult<BomVersionDto>.Fail("BOM version was not found.");
        }

        if (version.Status != BomVersionStatus.Draft)
        {
            return OperationResult<BomVersionDto>
                .Fail("Only Draft BOM versions can be changed.");
        }

        var patchError = ApplyPatch(version, command.Request);
        if (patchError is not null)
        {
            return OperationResult<BomVersionDto>.Fail(patchError);
        }

        version.BomDefinition.UpdatedBy = employeeId;
        version.BomDefinition.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<BomVersionDto>.Ok(
            BomMapper.ToWriteResponseDto(
                version.BomDefinition,
                version,
                version.Items.OrderBy(x => x.LineNo)));
    }

    private static string? ApplyPatch(
        BomVersion version,
        PatchBomVersionRequest request)
    {
        var clearFields = request.ClearFields
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clearFieldsError = BomRules.ValidatePatchClearFields(clearFields);
        if (clearFieldsError is not null)
        {
            return clearFieldsError;
        }

        if (request.BaseOutputQuantity is { } quantity)
        {
            if (quantity <= 0)
            {
                return "BaseOutputQuantity must be greater than zero.";
            }

            version.BaseOutputQuantity = quantity;
        }

        if (request.OutputUnit is not null)
        {
            var outputUnitError = BomRules.ValidateText(
                request.OutputUnit,
                32,
                nameof(request.OutputUnit),
                required: true);

            if (outputUnitError is not null)
            {
                return outputUnitError;
            }

            version.OutputUnit = request.OutputUnit.Trim();
        }

        ApplyEffectivePeriodPatch(version, request, clearFields);

        var periodError = BomRules.ValidatePeriod(
            version.EffectiveFrom,
            version.EffectiveTo);

        if (periodError is not null)
        {
            return periodError;
        }

        ApplyOptionalTextPatch(version, request, clearFields);
        return null;
    }

    private static void ApplyEffectivePeriodPatch(
        BomVersion version,
        PatchBomVersionRequest request,
        IReadOnlySet<string> clearFields)
    {
        if (request.EffectiveFrom is not null)
        {
            version.EffectiveFrom = request.EffectiveFrom;
        }

        if (request.EffectiveTo is not null)
        {
            version.EffectiveTo = request.EffectiveTo;
        }

        if (clearFields.Contains(BomPatchFields.EffectiveFrom))
        {
            version.EffectiveFrom = null;
        }

        if (clearFields.Contains(BomPatchFields.EffectiveTo))
        {
            version.EffectiveTo = null;
        }
    }

    private static void ApplyOptionalTextPatch(
        BomVersion version,
        PatchBomVersionRequest request,
        IReadOnlySet<string> clearFields)
    {
        if (request.ChangeReason is not null)
        {
            version.ChangeReason = BomRules.NormalizeOptionalText(request.ChangeReason);
        }

        if (request.Note is not null)
        {
            version.Note = BomRules.NormalizeOptionalText(request.Note);
        }

        if (clearFields.Contains(BomPatchFields.ChangeReason))
        {
            version.ChangeReason = null;
        }

        if (clearFields.Contains(BomPatchFields.Note))
        {
            version.Note = null;
        }
    }
}
