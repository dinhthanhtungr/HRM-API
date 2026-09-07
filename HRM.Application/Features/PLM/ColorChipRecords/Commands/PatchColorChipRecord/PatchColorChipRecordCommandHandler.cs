using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.ColorChipRecords.Commands.CreateColorChipRecord;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Application.Features.PLM.ColorChipRecords.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ColorChipRecords.Commands.PatchColorChipRecord;

internal sealed class PatchColorChipRecordCommandHandler
    : IRequestHandler<PatchColorChipRecordCommand, OperationResult<SaveColorChipRecordResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PatchColorChipRecordCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<SaveColorChipRecordResultDto>> Handle(
        PatchColorChipRecordCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ColorChipRecordId == Guid.Empty)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail("ColorChipRecordId is invalid.");
        }

        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (employeeId == Guid.Empty || companyId == Guid.Empty)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail("Current employee or company is invalid.");
        }

        var clearFieldsResult = ColorChipRecordPatchContract.ValidateAndNormalize(
            request.ClearFields,
            GetFieldsWithValues(request));
        if (!clearFieldsResult.Success)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                clearFieldsResult.Message ?? "ClearFields is invalid.");
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(validationError);
        }

        var formulaResult = ColorChipRecordRules.ValidateDevelopmentFormulaIds(request.DevelopmentFormulaIds);
        if (!formulaResult.Success)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                formulaResult.Message ?? "DevelopmentFormulaIds is invalid.");
        }

        var entity = await _dbContext.ColorChipRecords
            .AsTracking()
            .Include(x => x.DevelopmentFormulas)
            .FirstOrDefaultAsync(x =>
                x.ColorChipRecordId == request.ColorChipRecordId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Product != null &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive,
                cancellationToken);
        if (entity?.ProductId is not { } productId)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                "Color chip record was not found or is outside the current company.");
        }

        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDateWithDatabasePrecision(
            request.ExpectedUpdatedDate,
            entity.UpdatedDate,
            "Color chip record");
        if (concurrencyError is not null)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                concurrencyError);
        }

        if (request.DevelopmentFormulaIds is not null && formulaResult.Data is { } formulaId &&
            !await FormulaExistsAsync(formulaId, productId, companyId, cancellationToken))
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                "Development formula was not found or does not belong to this product and company.");
        }

        var changed = ApplyValues(entity, request);
        changed |= ApplyClears(entity, clearFieldsResult.Data!);
        if (request.DevelopmentFormulaIds is not null)
        {
            changed |= ApplyDevelopmentFormula(
                entity,
                formulaResult.Data,
                out var newDevelopmentFormulaLink);
            if (newDevelopmentFormulaLink is not null)
            {
                _dbContext.ColorChipRecordDevelopmentFormulas.Add(newDevelopmentFormulaLink);
            }
        }

        if (changed)
        {
            entity.UpdatedBy = employeeId;
            entity.UpdatedDate = _dateTimeProvider.Now;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                return OperationResult<SaveColorChipRecordResultDto>.Fail(
                    OptimisticConcurrencyHelper.CreateConflictMessage(
                        "Color chip record",
                        exception));
            }
        }

        return OperationResult<SaveColorChipRecordResultDto>.Ok(
            new SaveColorChipRecordResultDto
            {
                ColorChipRecordId = entity.ColorChipRecordId,
                ProductId = productId,
                AttachmentCollectionId = entity.AttachmentCollectionId,
                UpdatedDate = entity.UpdatedDate
            },
            changed ? "Updated color chip record successfully." : "No color chip record fields changed.");
    }

    private static bool ApplyValues(ColorChipRecord entity, PatchColorChipRecordCommand request)
    {
        var changed = false;
        changed |= PatchHelper.SetIfHasValue(request.RecordType, () => entity.RecordType, value => entity.RecordType = value);
        changed |= PatchHelper.SetIfHasValue(request.ResinType, () => entity.ResinType, value => entity.ResinType = value);
        changed |= PatchHelper.SetIfHasValue(request.LogoType, () => entity.LogoType, value => entity.LogoType = value);
        changed |= PatchHelper.SetIfHasValue(request.FormStyle, () => entity.FormStyle, value => entity.FormStyle = value);
        changed |= PatchHelper.SetTrimmed(request.Machine, () => entity.Machine, value => entity.Machine = value);
        changed |= PatchHelper.SetTrimmed(request.Resin, () => entity.Resin, value => entity.Resin = value);
        changed |= PatchHelper.SetTrimmed(request.TemperatureLimit, () => entity.TemperatureLimit, value => entity.TemperatureLimit = value);
        changed |= PatchHelper.SetTrimmed(request.SizeText, () => entity.SizeText, value => entity.SizeText = value);
        changed |= PatchHelper.SetTrimmed(request.NetWeightGram, () => entity.NetWeightGram, value => entity.NetWeightGram = value);
        changed |= PatchHelper.SetIfHasValue(request.Lightness, () => entity.Lightness, value => entity.Lightness = value);
        changed |= PatchHelper.SetIfHasValue(request.AValue, () => entity.AValue, value => entity.AValue = value);
        changed |= PatchHelper.SetIfHasValue(request.BValue, () => entity.BValue, value => entity.BValue = value);
        changed |= PatchHelper.SetTrimmed(request.Note, () => entity.Note, value => entity.Note = value);
        changed |= PatchHelper.SetTrimmed(request.PrintNote, () => entity.PrintNote, value => entity.PrintNote = value);

        if (request.PelletWeightGram.HasValue)
            changed |= PatchHelper.SetNullable(request.PelletWeightGram, () => entity.PelletWeightGram, value => entity.PelletWeightGram = value);
        if (request.Electrostatic.HasValue)
            changed |= PatchHelper.SetNullable(request.Electrostatic, () => entity.Electrostatic, value => entity.Electrostatic = value);
        if (request.RecordDate.HasValue)
            changed |= PatchHelper.SetNullable(request.RecordDate, () => entity.RecordDate, value => entity.RecordDate = value);

        return changed;
    }

    private static bool ApplyClears(ColorChipRecord entity, IReadOnlySet<string> clearFields)
    {
        var changed = false;
        foreach (var field in clearFields)
        {
            changed |= field.ToLowerInvariant() switch
            {
                "machine" => PatchHelper.SetNullableRef<string>(null, () => entity.Machine, value => entity.Machine = value),
                "resin" => PatchHelper.SetNullableRef<string>(null, () => entity.Resin, value => entity.Resin = value),
                "temperaturelimit" => PatchHelper.SetNullableRef<string>(null, () => entity.TemperatureLimit, value => entity.TemperatureLimit = value),
                "sizetext" => PatchHelper.SetNullableRef<string>(null, () => entity.SizeText, value => entity.SizeText = value),
                "pelletweightgram" => PatchHelper.SetNullable<decimal>(null, () => entity.PelletWeightGram, value => entity.PelletWeightGram = value),
                "netweightgram" => PatchHelper.SetNullableRef<string>(null, () => entity.NetWeightGram, value => entity.NetWeightGram = value),
                "electrostatic" => PatchHelper.SetNullable<bool>(null, () => entity.Electrostatic, value => entity.Electrostatic = value),
                "recorddate" => PatchHelper.SetNullable<DateTime>(null, () => entity.RecordDate, value => entity.RecordDate = value),
                "note" => PatchHelper.SetNullableRef<string>(null, () => entity.Note, value => entity.Note = value),
                "printnote" => PatchHelper.SetNullableRef<string>(null, () => entity.PrintNote, value => entity.PrintNote = value),
                _ => false
            };
        }

        return changed;
    }

    private static bool ApplyDevelopmentFormula(
        ColorChipRecord entity,
        Guid? formulaId,
        out ColorChipRecordDevelopmentFormula? newLink)
    {
        newLink = null;
        var changed = false;
        ColorChipRecordDevelopmentFormula? selectedLink = null;
        foreach (var link in entity.DevelopmentFormulas)
        {
            var shouldBeActive = formulaId.HasValue &&
                                 link.DevelopmentFormulaId == formulaId &&
                                 selectedLink is null;
            if (link.IsActive != shouldBeActive)
            {
                link.IsActive = shouldBeActive;
                changed = true;
            }

            if (shouldBeActive)
            {
                selectedLink = link;
            }
        }

        if (formulaId.HasValue && selectedLink is null)
        {
            newLink = new ColorChipRecordDevelopmentFormula
            {
                ColorChipRecordDevelopmentFormulaId = Guid.CreateVersion7(),
                ColorChipRecordId = entity.ColorChipRecordId,
                DevelopmentFormulaId = formulaId,
                IsActive = true
            };
            entity.DevelopmentFormulas.Add(newLink);
            changed = true;
        }

        return changed;
    }

    private Task<bool> FormulaExistsAsync(
        Guid formulaId,
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _dbContext.Formulas.AsNoTracking().AnyAsync(x =>
            x.FormulaId == formulaId &&
            x.ProductId == productId &&
            x.Product.CompanyId == companyId &&
            x.IsActive &&
            (!x.CompanyId.HasValue || x.CompanyId == companyId),
            cancellationToken);

    private static IEnumerable<string> GetFieldsWithValues(PatchColorChipRecordCommand request)
    {
        if (request.Machine is not null) yield return ColorChipRecordPatchFields.Machine;
        if (request.Resin is not null) yield return ColorChipRecordPatchFields.Resin;
        if (request.TemperatureLimit is not null) yield return ColorChipRecordPatchFields.TemperatureLimit;
        if (request.SizeText is not null) yield return ColorChipRecordPatchFields.SizeText;
        if (request.PelletWeightGram.HasValue) yield return ColorChipRecordPatchFields.PelletWeightGram;
        if (request.NetWeightGram is not null) yield return ColorChipRecordPatchFields.NetWeightGram;
        if (request.Electrostatic.HasValue) yield return ColorChipRecordPatchFields.Electrostatic;
        if (request.RecordDate.HasValue) yield return ColorChipRecordPatchFields.RecordDate;
        if (request.Note is not null) yield return ColorChipRecordPatchFields.Note;
        if (request.PrintNote is not null) yield return ColorChipRecordPatchFields.PrintNote;
    }

    private static string? ValidateRequest(PatchColorChipRecordCommand request)
    {
        if (request.RecordType.HasValue && !Enum.IsDefined(request.RecordType.Value)) return "RecordType is invalid.";
        if (request.ResinType.HasValue && !Enum.IsDefined(request.ResinType.Value)) return "ResinType is invalid.";
        if (request.LogoType.HasValue && !Enum.IsDefined(request.LogoType.Value)) return "LogoType is invalid.";
        if (request.FormStyle.HasValue && !Enum.IsDefined(request.FormStyle.Value)) return "FormStyle is invalid.";

        var error = ColorChipRecordRules.ValidateMeasurements(request.PelletWeightGram);
        return error ?? CreateColorChipRecordCommandHandler.ValidateTextFields(
            request.Machine,
            request.Resin,
            request.TemperatureLimit,
            request.SizeText,
            request.NetWeightGram,
            request.Note,
            request.PrintNote,
            rejectBlank: true);
    }
}
