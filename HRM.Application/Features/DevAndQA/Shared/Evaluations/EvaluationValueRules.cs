using System.Globalization;
using System.Text.Json;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations;

/// <summary>Chuẩn hóa số đo theo cấu hình nhập; không tự suy kết luận QC hoặc PO.</summary>
internal static class EvaluationValueRules
{
    public static string? Normalize(string? value, EvaluationConfigurationDto config)
    {
        if (value is null) return null;
        if (config.DataType == EvaluationDataType.Attachment)
            throw new EvaluationRequestException("Chỉ tiêu Attachment nhận file, không nhận Value; dùng clearValue khi đổi kiểu.");
        if (value.Length > 16000 || string.IsNullOrWhiteSpace(value))
            throw new EvaluationRequestException("Value rỗng hoặc quá dài; dùng clearValue để xóa kết quả.");
        value = value.Trim();
        using var document = config.InputConfigJson is null ? null : JsonDocument.Parse(config.InputConfigJson);
        var root = document?.RootElement;
        decimal Number(JsonElement x) => decimal.Parse(x.ValueKind == JsonValueKind.String ? x.GetString()! : x.GetRawText(), CultureInfo.InvariantCulture);
        switch (config.DataType)
        {
            case EvaluationDataType.Text:
                if (root is { } text && text.TryGetProperty("maxLength", out var length) && value.Length > length.GetInt32())
                    throw new EvaluationRequestException("Value vượt maxLength.");
                return value;
            case EvaluationDataType.Decimal:
            case EvaluationDataType.Integer:
                if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ||
                    config.DataType == EvaluationDataType.Integer && decimal.Truncate(number) != number)
                    throw new EvaluationRequestException("Value phải là số invariant hợp lệ.");
                if (root is { } numeric)
                {
                    if (numeric.TryGetProperty("minValue", out var min) && number < Number(min) ||
                        numeric.TryGetProperty("maxValue", out var max) && number > Number(max))
                        throw new EvaluationRequestException("Value ngoài miền nhập.");
                    if (numeric.TryGetProperty("decimalPlaces", out var places) && decimal.Round(number, places.GetInt32()) != number)
                        throw new EvaluationRequestException("Value vượt số chữ số thập phân cho phép.");
                }
                return number.ToString("G29", CultureInfo.InvariantCulture);
            case EvaluationDataType.Boolean:
                if (!bool.TryParse(value, out var boolean)) throw new EvaluationRequestException("Value phải là true/false.");
                return boolean ? "true" : "false";
            case EvaluationDataType.Date:
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    throw new EvaluationRequestException("Value phải là ngày yyyy-MM-dd.");
                if (root is { } dates &&
                    (dates.TryGetProperty("minDate", out var minDate) && date < DateOnly.ParseExact(minDate.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture) ||
                     dates.TryGetProperty("maxDate", out var maxDate) && date > DateOnly.ParseExact(maxDate.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)))
                    throw new EvaluationRequestException("Ngày ngoài miền nhập.");
                return value;
            case EvaluationDataType.SingleSelect:
            case EvaluationDataType.MultiSelect:
                if (root is not { } choices || !choices.TryGetProperty("options", out var options))
                    throw new EvaluationRequestException("Chỉ tiêu chưa cấu hình lựa chọn.");
                var allowed = options.EnumerateArray().Select(x => x.GetProperty("code").GetString()!).ToHashSet(StringComparer.Ordinal);
                if (config.DataType == EvaluationDataType.SingleSelect)
                {
                    if (!allowed.Contains(value)) throw new EvaluationRequestException("Mã lựa chọn không hợp lệ.");
                    return value;
                }
                try
                {
                    var codes = JsonSerializer.Deserialize<string[]>(value);
                    if (codes is null || codes.Any(x => x is null || !allowed.Contains(x)) || codes.Distinct().Count() != codes.Length)
                        throw new EvaluationRequestException("Danh sách lựa chọn không hợp lệ.");
                    return JsonSerializer.Serialize(codes);
                }
                catch (JsonException) { throw new EvaluationRequestException("MultiSelect phải là chuỗi JSON mảng mã."); }
            default: throw new EvaluationRequestException("DataType không hợp lệ.");
        }
    }
}
