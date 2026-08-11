using System.Globalization;
using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;

/// <summary>
/// Catalog field ổn định mà Sale được đề xuất và backend được phép áp dụng sau khi Lab duyệt.
/// </summary>
internal static class SampleRequestDataChangeFieldCatalog
{
    private const int MaxStringLength = 2000;

    private static readonly IReadOnlyDictionary<string, FieldDefinition> Definitions =
        new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["sample_request.expected_quantity"] = Number("sample_request.expected_quantity", "Expected quantity", x => x.ExpectedQuantity, (x, value) => x.ExpectedQuantity = value),
            ["sample_request.expected_price"] = Decimal("sample_request.expected_price", "Expected price", x => x.ExpectedPrice, (x, value) => x.ExpectedPrice = value),
            ["sample_request.sample_quantity"] = Number("sample_request.sample_quantity", "Sample quantity", x => x.SampleQuantity, (x, value) => x.SampleQuantity = value),
            ["sample_request.package"] = String("sample_request.package", "Package", x => x.Package, (x, value) => x.Package = value),
            ["sample_request.bag_weight"] = Integer("sample_request.bag_weight", "Bag weight", x => x.BagWeight, (x, value) => x.BagWeight = value),
            ["sample_request.customer_product_code"] = String("sample_request.customer_product_code", "Customer product code", x => x.CustomerProductCode, (x, value) => x.CustomerProductCode = value),
            ["sample_request.request_delivery_date"] = Date("sample_request.request_delivery_date", "Request delivery date", x => x.RequestDeliveryDate, (x, value) => x.RequestDeliveryDate = value),
            ["sample_request.expected_delivery_date"] = Date("sample_request.expected_delivery_date", "Expected delivery date", x => x.ExpectedDeliveryDate, (x, value) => x.ExpectedDeliveryDate = value),
            ["sample_request.request_test_sample_date"] = Date("sample_request.request_test_sample_date", "Request test sample date", x => x.RequestTestSampleDate, (x, value) => x.RequestTestSampleDate = value),
            ["sample_request.expected_price_quote_date"] = Date("sample_request.expected_price_quote_date", "Expected price quote date", x => x.ExpectedPriceQuoteDate, (x, value) => x.ExpectedPriceQuoteDate = value),
            ["sample_request.info_type"] = String("sample_request.info_type", "Info type", x => x.InfoType, (x, value) => x.InfoType = value),
            ["sample_request.other_comment"] = String("sample_request.other_comment", "Other comment", x => x.OtherComment, (x, value) => x.OtherComment = value),
            ["sample_request.sale_comment"] = String("sample_request.sale_comment", "Sale comment", x => x.SaleComment, (x, value) => x.SaleComment = value),
            ["sample_request.additional_comment"] = String("sample_request.additional_comment", "Additional comment", x => x.AdditionalComment, (x, value) => x.AdditionalComment = value),
            ["sample_request.request_type"] = String("sample_request.request_type", "Request type", x => x.RequestType, (x, value) => x.RequestType = value),

            ["product.colour_code"] = String("product.colour_code", "Colour code", x => x.Product.ColourCode, (x, value) => x.ColourCode = value),
            ["product.name"] = String("product.name", "Product name", x => x.Product.Name, (x, value) => x.ProductName = value),
            ["product.colour_name"] = String("product.colour_name", "Colour name", x => x.Product.ColourName, (x, value) => x.ColourName = value),
            ["product.additive"] = String("product.additive", "Additive", x => x.Product.Additive, (x, value) => x.Additive = value),
            ["product.usage_rate"] = Number("product.usage_rate", "Usage rate", x => x.Product.UsageRate, (x, value) => x.UsageRate = value),
            ["product.delta_e"] = String("product.delta_e", "Delta E", x => x.Product.DeltaE, (x, value) => x.DeltaE = value),
            ["product.requirement"] = String("product.requirement", "Product requirement", x => x.Product.Requirement, (x, value) => x.ProductRequirement = value),
            ["product.expiry_type"] = String("product.expiry_type", "Expiry type", x => x.Product.ExpiryType, (x, value) => x.ExpiryType = value),
            ["product.storage_condition"] = Boolean("product.storage_condition", "Storage condition", x => x.Product.StorageCondition, (x, value) => x.StorageCondition = value),
            ["product.lab_comment"] = String("product.lab_comment", "Lab comment", x => x.Product.LabComment, (x, value) => x.LabComment = value),
            ["product.procedure"] = String("product.procedure", "Procedure", x => x.Product.Procedure, (x, value) => x.Procedure = value),
            ["product.recycle_rate"] = Number("product.recycle_rate", "Recycle rate", x => x.Product.RecycleRate, (x, value) => x.RecycleRate = value),
            ["product.taical_rate"] = Number("product.taical_rate", "Taical rate", x => x.Product.TaicalRate, (x, value) => x.TaicalRate = value),
            ["product.application"] = String("product.application", "Application", x => x.Product.Application, (x, value) => x.Application = value),
            ["product.product_usage"] = String("product.product_usage", "Product usage", x => x.Product.ProductUsage, (x, value) => x.ProductUsage = value),
            ["product.polymer_matched_in"] = String("product.polymer_matched_in", "Polymer matched in", x => x.Product.PolymerMatchedIn, (x, value) => x.PolymerMatchedIn = value),
            ["product.code"] = String("product.code", "Product code", x => x.Product.Code, (x, value) => x.ProductCode = value),
            ["product.end_user"] = String("product.end_user", "End user", x => x.Product.EndUser, (x, value) => x.EndUser = value),
            ["product.food_safety"] = Boolean("product.food_safety", "Food safety", x => x.Product.FoodSafety, (x, value) => x.FoodSafety = value),
            ["product.rohs_standard"] = Boolean("product.rohs_standard", "RoHS standard", x => x.Product.RohsStandard, (x, value) => x.RohsStandard = value),
            ["product.reach_standard"] = Boolean("product.reach_standard", "REACH standard", x => x.Product.ReachStandard, (x, value) => x.ReachStandard = value),
            ["product.max_temp"] = Number("product.max_temp", "Max temp", x => x.Product.MaxTemp, (x, value) => x.MaxTemp = value),
            ["product.weather_resistance"] = String("product.weather_resistance", "Weather resistance", x => x.Product.WeatherResistance, (x, value) => x.WeatherResistance = value),
            ["product.light_condition"] = String("product.light_condition", "Light condition", x => x.Product.LightCondition, (x, value) => x.LightCondition = value),
            ["product.visual_test"] = String("product.visual_test", "Visual test", x => x.Product.VisualTest, (x, value) => x.VisualTest = value),
            ["product.return_sample"] = Boolean("product.return_sample", "Return sample", x => x.Product.ReturnSample, (x, value) => x.ReturnSample = value),
            ["product.is_recycle"] = Boolean("product.is_recycle", "Is recycle", x => x.Product.IsRecycle, (x, value) => x.IsRecycle = value),
            ["product.weight"] = Number("product.weight", "Weight", x => x.Product.Weight, (x, value) => x.Weight = value),
            ["product.unit"] = String("product.unit", "Unit", x => x.Product.Unit, (x, value) => x.Unit = value),
            ["product.other_comment"] = String("product.other_comment", "Product other comment", x => x.Product.OtherComment, (x, value) => x.ProductOtherComment = value)
        };

    public static bool TryCreateProposal(
        SampleRequest sampleRequest,
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

        var oldValue = JsonSerializer.SerializeToElement(definition.ReadCurrent(sampleRequest));
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

    public static bool CurrentValueMatches(SampleRequest sampleRequest, SampleRequestDataChangeFieldPayload change)
    {
        return Definitions.TryGetValue(change.FieldCode, out var definition) &&
               JsonElementEquals(
                   JsonSerializer.SerializeToElement(definition.ReadCurrent(sampleRequest)),
                   change.OldValue);
    }

    private static FieldDefinition String(
        string code,
        string label,
        Func<SampleRequest, string?> read,
        Action<PatchSampleRequestCommand, string?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            sampleRequest => read(sampleRequest),
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
        Func<SampleRequest, double?> read,
        Action<PatchSampleRequestCommand, double?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            sampleRequest => read(sampleRequest),
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

    private static FieldDefinition Decimal(
        string code,
        string label,
        Func<SampleRequest, decimal?> read,
        Action<PatchSampleRequestCommand, decimal?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            sampleRequest => read(sampleRequest),
            (JsonElement value, out JsonElement normalized, out string? error) =>
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var parsed))
                {
                    normalized = default;
                    error = "value must be a number.";
                    return false;
                }

                normalized = JsonSerializer.SerializeToElement(parsed);
                error = null;
                return true;
            },
            (patch, value) => apply(patch, value.GetDecimal()));
    }

    private static FieldDefinition Integer(
        string code,
        string label,
        Func<SampleRequest, int> read,
        Action<PatchSampleRequestCommand, int?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            sampleRequest => read(sampleRequest),
            (JsonElement value, out JsonElement normalized, out string? error) =>
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
                {
                    normalized = default;
                    error = "value must be an integer.";
                    return false;
                }

                normalized = JsonSerializer.SerializeToElement(parsed);
                error = null;
                return true;
            },
            (patch, value) => apply(patch, value.GetInt32()));
    }

    private static FieldDefinition Date(
        string code,
        string label,
        Func<SampleRequest, DateTime?> read,
        Action<PatchSampleRequestCommand, DateTime?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            sampleRequest => read(sampleRequest),
            (JsonElement value, out JsonElement normalized, out string? error) =>
            {
                if (value.ValueKind != JsonValueKind.String ||
                    !DateTime.TryParse(
                        value.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var parsed))
                {
                    normalized = default;
                    error = "value must be a valid date string.";
                    return false;
                }

                normalized = JsonSerializer.SerializeToElement(parsed);
                error = null;
                return true;
            },
            (patch, value) => apply(patch, value.GetDateTime()));
    }

    private static FieldDefinition Boolean(
        string code,
        string label,
        Func<SampleRequest, bool?> read,
        Action<PatchSampleRequestCommand, bool?> apply)
    {
        return new FieldDefinition(
            code,
            label,
            sampleRequest => read(sampleRequest),
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
        Func<SampleRequest, object?> ReadCurrent,
        TryNormalizeValue TryNormalize,
        Action<PatchSampleRequestCommand, JsonElement> ApplyToPatch);
}
