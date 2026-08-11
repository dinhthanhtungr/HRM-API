using System.Globalization;
using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;

/// <summary>
/// Danh mục field kỹ thuật mà Sale được đề xuất và backend được phép áp dụng sau khi Lab duyệt.
/// Mã field là contract ổn định; không nhận tên property C# tùy ý từ frontend.
/// </summary>
internal static class SampleRequestDataChangeFieldCatalogLegacy
{
    private const int MaxStringLength = 2000;

    private static readonly IReadOnlyDictionary<string, FieldDefinition> Definitions =
        new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["product.colour_code"] = String("product.colour_code", "Mã màu", x => x.ColourCode, (x, value) => x.ColourCode = value),
            ["product.name"] = String("product.name", "Tên sản phẩm", x => x.Name, (x, value) => x.ProductName = value),
            ["product.colour_name"] = String("product.colour_name", "Tên màu", x => x.ColourName, (x, value) => x.ColourName = value),
            ["product.additive"] = String("product.additive", "Phụ gia", x => x.Additive, (x, value) => x.Additive = value),
            ["product.usage_rate"] = Number("product.usage_rate", "Tỷ lệ sử dụng", x => x.UsageRate, (x, value) => x.UsageRate = value),
            ["product.delta_e"] = String("product.delta_e", "Delta E", x => x.DeltaE, (x, value) => x.DeltaE = value),
            ["product.requirement"] = String("product.requirement", "Yêu cầu sản phẩm", x => x.Requirement, (x, value) => x.ProductRequirement = value),
            ["product.expiry_type"] = String("product.expiry_type", "Loại hạn sử dụng", x => x.ExpiryType, (x, value) => x.ExpiryType = value),
            ["product.storage_condition"] = Boolean("product.storage_condition", "Điều kiện lưu trữ", x => x.StorageCondition, (x, value) => x.StorageCondition = value),
            ["product.lab_comment"] = String("product.lab_comment", "Ghi chú Lab", x => x.LabComment, (x, value) => x.LabComment = value),
            ["product.procedure"] = String("product.procedure", "Quy trình", x => x.Procedure, (x, value) => x.Procedure = value),
            ["product.recycle_rate"] = Number("product.recycle_rate", "Tỷ lệ tái chế", x => x.RecycleRate, (x, value) => x.RecycleRate = value),
            ["product.taical_rate"] = Number("product.taical_rate", "Tỷ lệ taical", x => x.TaicalRate, (x, value) => x.TaicalRate = value),
            ["product.application"] = String("product.application", "Ứng dụng", x => x.Application, (x, value) => x.Application = value),
            ["product.product_usage"] = String("product.product_usage", "Mục đích sử dụng", x => x.ProductUsage, (x, value) => x.ProductUsage = value),
            ["product.polymer_matched_in"] = String("product.polymer_matched_in", "Polymer tương thích", x => x.PolymerMatchedIn, (x, value) => x.PolymerMatchedIn = value),
            ["product.code"] = String("product.code", "Mã sản phẩm", x => x.Code, (x, value) => x.ProductCode = value),
            ["product.end_user"] = String("product.end_user", "Người dùng cuối", x => x.EndUser, (x, value) => x.EndUser = value),
            ["product.food_safety"] = Boolean("product.food_safety", "An toàn thực phẩm", x => x.FoodSafety, (x, value) => x.FoodSafety = value),
            ["product.rohs_standard"] = Boolean("product.rohs_standard", "Tiêu chuẩn RoHS", x => x.RohsStandard, (x, value) => x.RohsStandard = value),
            ["product.reach_standard"] = Boolean("product.reach_standard", "Tiêu chuẩn REACH", x => x.ReachStandard, (x, value) => x.ReachStandard = value),
            ["product.max_temp"] = Number("product.max_temp", "Nhiệt độ tối đa", x => x.MaxTemp, (x, value) => x.MaxTemp = value),
            ["product.weather_resistance"] = String("product.weather_resistance", "Kháng thời tiết", x => x.WeatherResistance, (x, value) => x.WeatherResistance = value),
            ["product.light_condition"] = String("product.light_condition", "Điều kiện ánh sáng", x => x.LightCondition, (x, value) => x.LightCondition = value),
            ["product.visual_test"] = String("product.visual_test", "Kiểm tra ngoại quan", x => x.VisualTest, (x, value) => x.VisualTest = value),
            ["product.return_sample"] = Boolean("product.return_sample", "Trả mẫu", x => x.ReturnSample, (x, value) => x.ReturnSample = value),
            ["product.is_recycle"] = Boolean("product.is_recycle", "Sản phẩm tái chế", x => x.IsRecycle, (x, value) => x.IsRecycle = value),
            ["product.weight"] = Number("product.weight", "Khối lượng", x => x.Weight, (x, value) => x.Weight = value),
            ["product.unit"] = String("product.unit", "Đơn vị", x => x.Unit, (x, value) => x.Unit = value),
            ["product.other_comment"] = String("product.other_comment", "Ghi chú sản phẩm", x => x.OtherComment, (x, value) => x.ProductOtherComment = value)
        };

    public static bool TryCreateProposal(
        Product product,
        SampleRequestProposedChangeRequestDto requestedChange,
        out SampleRequestDataChangeFieldPayload proposal,
        out string? error)
    {
        proposal = new SampleRequestDataChangeFieldPayload();
        error = null;

        var fieldCode = requestedChange.FieldCode?.Trim();
        if (string.IsNullOrWhiteSpace(fieldCode) || !Definitions.TryGetValue(fieldCode, out var definition))
        {
            error = $"Field '{fieldCode}' is not supported for a data change request.";
            return false;
        }

        if (!definition.TryNormalize(requestedChange.NewValue, out var normalizedValue, out error))
        {
            error = $"{definition.Label}: {error}";
            return false;
        }

        var oldValue = JsonSerializer.SerializeToElement(definition.ReadCurrent(product));
        if (JsonElementEquals(oldValue, normalizedValue))
        {
            error = $"{definition.Label} has not changed.";
            return false;
        }

        proposal = new SampleRequestDataChangeFieldPayload
        {
            FieldCode = definition.Code,
            Label = definition.Label,
            OldValue = oldValue,
            NewValue = normalizedValue,
            Status = SampleRequestDataChangeStatuses.Pending
        };
        return true;
    }

    public static bool TryApplyToPatch(
        PatchSampleRequestCommand patch,
        IReadOnlyCollection<SampleRequestDataChangeFieldPayload> changes,
        out string? error)
    {
        error = null;
        patch.IsDataChangeApproval = true;

        foreach (var change in changes)
        {
            if (!Definitions.TryGetValue(change.FieldCode, out var definition))
            {
                error = $"Field '{change.FieldCode}' is no longer supported.";
                return false;
            }

            definition.ApplyToPatch(patch, change.NewValue);
        }

        return true;
    }

    public static bool CurrentValueMatches(Product product, SampleRequestDataChangeFieldPayload change)
    {
        return Definitions.TryGetValue(change.FieldCode, out var definition) &&
               JsonElementEquals(
                   JsonSerializer.SerializeToElement(definition.ReadCurrent(product)),
                   change.OldValue);
    }

    private static FieldDefinition String(
        string code,
        string label,
        Func<Product, string?> read,
        Action<PatchSampleRequestCommand, string?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            product => read(product),
            (JsonElement value, out JsonElement normalized, out string? error) =>
            {
                error = null;
                if (value.ValueKind == JsonValueKind.Null)
                {
                    normalized = JsonSerializer.SerializeToElement<string?>(null);
                    return true;
                }

                if (value.ValueKind != JsonValueKind.String)
                {
                    normalized = default;
                    error = "value must be a string or null.";
                    return false;
                }

                var parsed = value.GetString()?.Trim();
                if (parsed is { Length: > MaxStringLength })
                {
                    normalized = default;
                    error = $"value cannot exceed {MaxStringLength} characters.";
                    return false;
                }

                normalized = JsonSerializer.SerializeToElement(parsed);
                return true;
            },
            (patch, value) => apply(
                patch,
                value.ValueKind == JsonValueKind.Null ? string.Empty : value.GetString()));
    }

    private static FieldDefinition Number(
        string code,
        string label,
        Func<Product, double?> read,
        Action<PatchSampleRequestCommand, double?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            product => read(product),
            (JsonElement value, out JsonElement normalized, out string? error) =>
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var parsed))
                {
                    normalized = default;
                    error = "value must be a number.";
                    return false;
                }

                normalized = JsonSerializer.SerializeToElement(parsed);
                error = null;
                return true;
            },
            (patch, value) => apply(patch, value.GetDouble()));
    }

    private static FieldDefinition Boolean(
        string code,
        string label,
        Func<Product, bool?> read,
        Action<PatchSampleRequestCommand, bool?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            product => read(product),
            (JsonElement value, out JsonElement normalized, out string? error) =>
            {
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    normalized = default;
                    error = "value must be true or false.";
                    return false;
                }

                normalized = JsonSerializer.SerializeToElement(value.GetBoolean());
                error = null;
                return true;
            },
            (patch, value) => apply(patch, value.GetBoolean()));
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
        => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);

    private delegate bool TryNormalizeValue(
        JsonElement value,
        out JsonElement normalized,
        out string? error);

    private sealed record FieldDefinition(
        string Code,
        string Label,
        Func<Product, object?> ReadCurrent,
        TryNormalizeValue TryNormalize,
        Action<PatchSampleRequestCommand, JsonElement> ApplyToPatch);
}
