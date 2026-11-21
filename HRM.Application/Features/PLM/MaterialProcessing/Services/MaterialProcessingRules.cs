using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Domain.Enums.Manufacturings;
namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

public static class MaterialProcessingRules
{
    // Plain numeric roots (NVL_NH_009) are valid; only a second numeric segment is a revision.
    public const string ExistingSuffixPattern = @"[-_](?:[BNLbnl]|[Rr][0-9]+)$|[-_][0-9]+[-_][0-9]+$";
    public static bool IsRootCode([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? code) => !string.IsNullOrWhiteSpace(code)
        && !System.Text.RegularExpressions.Regex.IsMatch(code.Trim(), ExistingSuffixPattern);
    public const decimal MaximumQuantity = 999999999999m;

    public static decimal[] Ratios(IReadOnlyList<decimal> quantities)
    {
        var total = quantities.Sum();
        if (quantities.Count == 0 || total <= 0 || quantities.Any(x => x <= 0)) return [];
        const decimal scale = 10000000000m;
        var exact = quantities.Select(x => x / total * scale).ToArray();
        var units = exact.Select(decimal.Floor).ToArray();
        var remainder = (int)(scale - units.Sum());
        foreach (var index in Enumerable.Range(0, units.Length).OrderByDescending(i => exact[i] - units[i]).ThenBy(i => i).Take(remainder))
            units[index]++;
        return units.Select(x => x / scale).ToArray();
    }

    public static string? Validate(SaveMaterialProcessingRequest request)
    {
        if (request.ProcessingType is not ("B" or "N" or "L")) return "InvalidType";
        if (request.MaterialId == Guid.Empty) return "InvalidOutput";
        if (!ValidQuantity(request.TotalQuantityRequest)) return "InvalidQuantity";
        if (request.NumOfBatches is <= 0) return "InvalidBatches";
        if (request.StepOfProduct.HasValue && !Enum.IsDefined(request.StepOfProduct.Value)) return "InvalidStep";
        if (!ValidStatus(request.Status)) return "InvalidStatus";
        if (request.Note?.Length > 2000) return "InvalidNote";
        if (request.QcCheck?.Length > 2000) return "InvalidNote";
        if (request.Details is null || request.Details.Count is < 1 or > 200) return "InvalidLines";
        if (request.Details.Any(x => x is null || x.MaterialId == Guid.Empty)) return "InvalidLines";
        if (request.Details.Any(x => x.ItemType is not (HRM.Domain.Enums.Formulas.ItemType.Material or HRM.Domain.Enums.Formulas.ItemType.MaterialFailure))) return "InvalidItemType";
        if (request.Details.Any(x => !ValidQuantity(x.QuantityKg))) return "InvalidQuantity";
        if (request.Details.Any(x => x.LotNo?.Length > 128)) return "InvalidLot";
        if (request.Details.Select(x => (x.MaterialId, x.ItemType, Lot: x.LotNo?.Trim().ToUpperInvariant() ?? "")).Distinct().Count() != request.Details.Count) return "DuplicateLines";
        if (request.TotalQuantityRequest > request.Details.Sum(x => x.QuantityKg)) return "OutputExceedsInput";
        return null;
    }
    public static bool ValidStatus(string? status)
        => Enum.TryParse<ManufacturingProductOrder>(status, out var parsed) &&
           Enum.IsDefined(parsed) && parsed != ManufacturingProductOrder.Unknown && parsed.ToString() == status;
    public static bool CanConfirm(string status)
        => status == ManufacturingProductOrder.New.ToString() || status == ManufacturingProductOrder.FormulaRequested.ToString();
    public static bool ValidQuantity(decimal value)
        => value > 0 && value <= MaximumQuantity && decimal.Round(value, 3) == value;
    public static string OutputCode(string sourceCode, string processingType) => BaseCode(sourceCode) + "-" + processingType;
    public static string BaseCode(string code)
    {
        var trimmed = code.Trim();
        return trimmed.Length > 2 && trimmed[^2] is '-' or '_' && "BNL".Contains(char.ToUpperInvariant(trimmed[^1]))
            ? trimmed[..^2] : trimmed;
    }
    public static string ProcessName(string type) => type switch { "B" => "Băm", "N" => "Nghiền", "L" => "Trộn", _ => throw new ArgumentOutOfRangeException(nameof(type)) };
    public static string? InternalColourName(string type) => type switch
    {
        "B" => "BĂM-NB",
        "N" => "NGHIỀN-NB",
        _ => null
    };
    public static HRM.Domain.Enums.Manufacturings.StepOfProduct Step(string type) => type switch
    {
        "B" => HRM.Domain.Enums.Manufacturings.StepOfProduct.BTS_GH,
        "N" => HRM.Domain.Enums.Manufacturings.StepOfProduct.BN_GH,
        "L" => HRM.Domain.Enums.Manufacturings.StepOfProduct.CT_GH,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
