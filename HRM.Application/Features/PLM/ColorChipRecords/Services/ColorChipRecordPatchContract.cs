using HRM.Application.Commons.Models;

namespace HRM.Application.Features.PLM.ColorChipRecords.Services;

public static class ColorChipRecordPatchFields
{
    public const string Machine = "machine";
    public const string Resin = "resin";
    public const string TemperatureLimit = "temperatureLimit";
    public const string SizeText = "sizeText";
    public const string PelletWeightGram = "pelletWeightGram";
    public const string NetWeightGram = "netWeightGram";
    public const string Electrostatic = "electrostatic";
    public const string RecordDate = "recordDate";
    public const string Note = "note";
    public const string PrintNote = "printNote";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Machine,
        Resin,
        TemperatureLimit,
        SizeText,
        PelletWeightGram,
        NetWeightGram,
        Electrostatic,
        RecordDate,
        Note,
        PrintNote
    };
}

internal static class ColorChipRecordPatchContract
{
    public static OperationResult<IReadOnlySet<string>> ValidateAndNormalize(
        IReadOnlyList<string>? requestedClearFields,
        IEnumerable<string> fieldsWithValues)
    {
        var clearFields = new HashSet<string>(
            requestedClearFields ?? [],
            StringComparer.OrdinalIgnoreCase);
        if (clearFields.Any(string.IsNullOrWhiteSpace))
        {
            return OperationResult<IReadOnlySet<string>>.Fail("clearFields cannot contain blank values.");
        }

        var unsupported = clearFields
            .Where(field => !ColorChipRecordPatchFields.Allowed.Contains(field))
            .OrderBy(field => field)
            .ToArray();
        if (unsupported.Length > 0)
        {
            return OperationResult<IReadOnlySet<string>>.Fail(
                $"Unsupported clearFields: {string.Join(", ", unsupported)}.");
        }

        var suppliedFields = new HashSet<string>(fieldsWithValues, StringComparer.OrdinalIgnoreCase);
        var conflicts = clearFields.Where(suppliedFields.Contains).OrderBy(field => field).ToArray();
        return conflicts.Length == 0
            ? OperationResult<IReadOnlySet<string>>.Ok(clearFields)
            : OperationResult<IReadOnlySet<string>>.Fail(
                $"Fields cannot be updated and cleared in the same request: {string.Join(", ", conflicts)}.");
    }
}
