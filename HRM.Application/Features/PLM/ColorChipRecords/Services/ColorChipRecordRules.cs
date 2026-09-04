using HRM.Application.Commons.Models;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.ColorChipRecords.Services;

internal static class ColorChipRecordRules
{
    public const int MaxTechnicalTextLength = 200;
    public const int MaxSizeTextLength = 100;
    public const int MaxNoteLength = 2000;

    public static string? ValidateClassification(
        RecordType recordType,
        ResinType resinType,
        LogoType logoType,
        FormStyle formStyle)
    {
        if (!Enum.IsDefined(recordType)) return "RecordType is invalid.";
        if (!Enum.IsDefined(resinType)) return "ResinType is invalid.";
        if (!Enum.IsDefined(logoType)) return "LogoType is invalid.";
        if (!Enum.IsDefined(formStyle)) return "FormStyle is invalid.";
        return null;
    }

    public static string? ValidateOptionalText(string fieldName, string? value, int maxLength, bool rejectBlank)
    {
        if (value is null) return null;
        if (rejectBlank && string.IsNullOrWhiteSpace(value))
        {
            return $"{fieldName} cannot be blank. Use clearFields to remove it.";
        }

        return value.Trim().Length > maxLength
            ? $"{fieldName} cannot exceed {maxLength} characters."
            : null;
    }

    public static OperationResult<Guid?> ValidateDevelopmentFormulaIds(IReadOnlyList<Guid>? formulaIds)
    {
        if (formulaIds is null)
        {
            return new OperationResult<Guid?> { Success = true };
        }

        var normalized = formulaIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (normalized.Length != formulaIds.Count)
        {
            return OperationResult<Guid?>.Fail("DevelopmentFormulaIds contains an empty or duplicate id.");
        }

        return normalized.Length switch
        {
            0 => new OperationResult<Guid?> { Success = true },
            1 => OperationResult<Guid?>.Ok(normalized[0]),
            _ => OperationResult<Guid?>.Fail("Only one development formula can be linked to a color chip record.")
        };
    }

    public static string? ValidateMeasurements(decimal? pelletWeightGram)
        => pelletWeightGram < 0m ? "PelletWeightGram cannot be negative." : null;

    public static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
