using System.Globalization;
using System.Text.Json;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;

/// <summary>Validate cấu hình mặc định của chỉ tiêu; không suy kết luận QC hoặc sửa snapshot đã tạo.</summary>
internal static class EvaluationStandardDefaultsRules
{
    private static readonly string[] Clearable = ["nameEn", "description", "unit", "measurementMethodId", "testMethod", "inputConfigJson", "acceptanceRuleJson", "specificationText"];

    public static string? ValidateClearFields(PatchEvaluationStandardCommand request)
    {
        if (request.ClearFields is null || request.ClearFields.Any(x => !Clearable.Contains(x)))
            return "clearFields chứa field không được phép.";

        foreach (var field in request.ClearFields)
        {
            var value = field switch
            {
                "measurementMethodId" => request.MeasurementMethodId?.ToString(),
                "nameEn" => request.NameEn,
                "description" => request.Description,
                "unit" => request.Unit,
                "testMethod" => request.TestMethod,
                "inputConfigJson" => request.InputConfigJson,
                "acceptanceRuleJson" => request.AcceptanceRuleJson,
                "specificationText" => request.SpecificationText,
                _ => null
            };
            if (value is not null) return $"Không vừa gửi value vừa clear {field}.";
        }
        return null;
    }

    public static string? Validate(EvaluationDataType type, string? unit, string? inputJson, string? ruleJson)
    {
        if (!Enum.IsDefined(type)) return "DataType không hợp lệ.";
        if (type == EvaluationDataType.Attachment && !string.IsNullOrWhiteSpace(ruleJson))
            return "Attachment không hỗ trợ acceptanceRuleJson.";
        if (unit?.Trim().Length > 50) return "Unit tối đa 50 ký tự.";
        if (inputJson?.Length > 16000 || ruleJson?.Length > 16000) return "Cấu hình JSON tối đa 16000 ký tự.";
        try
        {
            using var input = Parse(inputJson);
            using var rule = Parse(ruleJson);
            var options = new HashSet<string>(StringComparer.Ordinal);
            var error = ValidateInput(type, input?.RootElement, options);
            return error ?? ValidateRule(type, rule?.RootElement, options);
        }
        catch (JsonException) { return "Cấu hình phải là JSON object hợp lệ."; }
    }

    public static string? NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.EnumerateObject().Select(x => x.Name).Distinct().Count() != document.RootElement.EnumerateObject().Count())
        {
            document.Dispose();
            throw new JsonException();
        }
        return document;
    }

    private static string? ValidateInput(EvaluationDataType type, JsonElement? config, HashSet<string> options)
    {
        if (config is null)
            return type is EvaluationDataType.SingleSelect or EvaluationDataType.MultiSelect ? "Kiểu lựa chọn cần inputConfigJson.options." : null;

        var root = config.Value;
        string[] allowed = type switch
        {
            EvaluationDataType.Decimal => ["minValue", "maxValue", "decimalPlaces"],
            EvaluationDataType.Integer => ["minValue", "maxValue"],
            EvaluationDataType.Text => ["maxLength"],
            EvaluationDataType.Date => ["minDate", "maxDate"],
            EvaluationDataType.SingleSelect or EvaluationDataType.MultiSelect => ["options"],
            EvaluationDataType.Attachment => ["maxFiles", "maxFileSizeBytes", "allowedExtensions"],
            _ => []
        };
        if (root.EnumerateObject().Any(x => !allowed.Contains(x.Name))) return "InputConfig có field không phù hợp DataType.";
        if (type == EvaluationDataType.Attachment)
            return HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments.EvaluationAttachmentRules.ValidateConfiguration(root);
        if (type is EvaluationDataType.Decimal or EvaluationDataType.Integer)
        {
            decimal? min = null, max = null;
            if (root.TryGetProperty("minValue", out var v))
            {
                if (!Number(v, out var n) || type == EvaluationDataType.Integer && n != decimal.Truncate(n)) return "minValue không hợp lệ.";
                min = n;
            }
            if (root.TryGetProperty("maxValue", out v))
            {
                if (!Number(v, out var n) || type == EvaluationDataType.Integer && n != decimal.Truncate(n)) return "maxValue không hợp lệ.";
                max = n;
            }
            if (min > max) return "minValue không được lớn hơn maxValue.";
            if (root.TryGetProperty("decimalPlaces", out v) && (!v.TryGetInt32Safe(out var places) || places is < 0 or > 28))
                return "decimalPlaces phải là số nguyên từ 0 đến 28.";
        }
        if (root.TryGetProperty("maxLength", out var length) && (!length.TryGetInt32Safe(out var nlength) || nlength <= 0))
            return "maxLength phải là số nguyên dương.";
        if (type == EvaluationDataType.Date)
        {
            DateOnly? min = null, max = null;
            if (root.TryGetProperty("minDate", out var date))
            { if (!Date(date, out var d)) return "minDate phải có dạng yyyy-MM-dd."; min = d; }
            if (root.TryGetProperty("maxDate", out date))
            { if (!Date(date, out var d)) return "maxDate phải có dạng yyyy-MM-dd."; max = d; }
            if (min > max) return "minDate không được lớn hơn maxDate.";
        }
        if (type is EvaluationDataType.SingleSelect or EvaluationDataType.MultiSelect)
        {
            if (!root.TryGetProperty("options", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                return "options phải là mảng lựa chọn không rỗng.";
            foreach (var option in list.EnumerateArray())
            {
                if (option.ValueKind != JsonValueKind.Object || option.EnumerateObject().Count() != 2 ||
                    !option.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(code.GetString()) ||
                    !option.TryGetProperty("label", out var label) || label.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(label.GetString()) ||
                    !options.Add(code.GetString()!)) return "Mỗi option cần code duy nhất và label không rỗng.";
            }
        }
        return null;
    }

    private static string? ValidateRule(EvaluationDataType type, JsonElement? config, HashSet<string> options)
    {
        if (config is null) return null;
        var root = config.Value;
        if (!root.TryGetProperty("operator", out var op) || op.ValueKind != JsonValueKind.String) return "AcceptanceRule cần operator.";
        var name = op.GetString();
        var ordered = type is EvaluationDataType.Decimal or EvaluationDataType.Integer or EvaluationDataType.Date;
        if (name == "Between" && ordered)
        {
            if (root.EnumerateObject().Count() != 3 || !root.TryGetProperty("min", out var min) || !root.TryGetProperty("max", out var max) ||
                !Value(type, min, options) || !Value(type, max, options)) return "Between cần min/max đúng kiểu dữ liệu.";
            if (type == EvaluationDataType.Date)
            { Date(min, out var a); Date(max, out var b); if (a > b) return "Between min > max."; }
            else { Number(min, out var a); Number(max, out var b); if (a > b) return "Between min > max."; }
            return null;
        }
        if (name is "In" or "NotIn" && type is EvaluationDataType.SingleSelect or EvaluationDataType.MultiSelect)
        {
            if (root.EnumerateObject().Count() != 2 || !root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array ||
                values.GetArrayLength() == 0 || values.EnumerateArray().Any(x => !Value(EvaluationDataType.SingleSelect, x, options)))
                return "In/NotIn cần values chứa mã option hợp lệ.";
            return null;
        }
        if (name is not ("Equal" or "NotEqual") && !(ordered && name is "LessThan" or "LessThanOrEqual" or "GreaterThan" or "GreaterThanOrEqual"))
            return "Operator không phù hợp DataType.";
        if (type == EvaluationDataType.MultiSelect || root.EnumerateObject().Count() != 2 ||
            !root.TryGetProperty("value", out var value) || !Value(type, value, options)) return "AcceptanceRule value không đúng kiểu dữ liệu.";
        return null;
    }

    private static bool Value(EvaluationDataType type, JsonElement value, HashSet<string> options) => type switch
    {
        EvaluationDataType.Decimal => Number(value, out _),
        EvaluationDataType.Integer => Number(value, out var n) && n == decimal.Truncate(n),
        EvaluationDataType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        EvaluationDataType.Date => Date(value, out _),
        EvaluationDataType.SingleSelect => value.ValueKind == JsonValueKind.String && options.Contains(value.GetString()!),
        EvaluationDataType.Text => value.ValueKind == JsonValueKind.String,
        _ => false
    };

    private static bool Number(JsonElement value, out decimal number) => decimal.TryParse(
        value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText(),
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
        CultureInfo.InvariantCulture, out number);

    private static bool Date(JsonElement value, out DateOnly date) => DateOnly.TryParseExact(
        value.ValueKind == JsonValueKind.String ? value.GetString() : null,
        "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryGetInt32Safe(this JsonElement value, out int number)
    {
        number = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number);
    }
}
