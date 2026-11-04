using System.Text.Json;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;

/// <summary>Whitelist file đánh giá; giới hạn template không được vượt giới hạn server.</summary>
internal static class EvaluationAttachmentRules
{
    public const int MaxFiles = 20;
    public const long MaxBytes = 10_000_000;
    private static readonly IReadOnlyDictionary<string, string[]> MimeTypes = new Dictionary<string, string[]>
    {
        [".jpg"] = ["image/jpeg"], [".jpeg"] = ["image/jpeg"], [".png"] = ["image/png"],
        [".webp"] = ["image/webp"], [".gif"] = ["image/gif"], [".pdf"] = ["application/pdf"],
        [".txt"] = ["text/plain"], [".csv"] = ["text/csv", "application/vnd.ms-excel"],
        [".doc"] = ["application/msword"], [".xls"] = ["application/vnd.ms-excel"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]
    };

    public static string? ValidateConfiguration(JsonElement root)
    {
        if (root.TryGetProperty("maxFiles", out var count) &&
            (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var n) || n is < 1 or > MaxFiles))
            return "maxFiles phải từ 1 đến 20.";
        if (root.TryGetProperty("maxFileSizeBytes", out var size) &&
            (size.ValueKind != JsonValueKind.Number || !size.TryGetInt64(out var bytes) || bytes is < 1 or > MaxBytes))
            return "maxFileSizeBytes phải từ 1 đến 10000000.";
        if (root.TryGetProperty("allowedExtensions", out var list) &&
            (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() < 1 || list.GetArrayLength() > MimeTypes.Count ||
             list.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String || !MimeTypes.ContainsKey(x.GetString()!.ToLowerInvariant()))))
            return "allowedExtensions chứa extension không được hỗ trợ.";
        return null;
    }

    public static void Validate(EvaluationConfigurationDto config, IReadOnlyList<AttachmentUploadFile> files, int retainedCount)
    {
        if ((files.Count > 0 || retainedCount > 0) && config.DataType != EvaluationDataType.Attachment)
            throw new EvaluationRequestException("Chỉ kiểu Attachment được đính kèm file.");
        using var doc = config.InputConfigJson is null ? null : JsonDocument.Parse(config.InputConfigJson);
        var root = doc?.RootElement;
        var maxFiles = root is { } r && r.TryGetProperty("maxFiles", out var count) ? count.GetInt32() : MaxFiles;
        var maxBytes = root is { } s && s.TryGetProperty("maxFileSizeBytes", out var size) ? size.GetInt64() : MaxBytes;
        var extensions = root is { } t && t.TryGetProperty("allowedExtensions", out var list)
            ? list.EnumerateArray().Select(x => x.GetString()!.ToLowerInvariant()).ToHashSet() : null;
        if (files.Count + retainedCount > maxFiles) throw new EvaluationRequestException("Số file vượt giới hạn chỉ tiêu.");
        foreach (var file in files)
        {
            var name = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
                throw new EvaluationRequestException("Tên file không hợp lệ hoặc vượt 255 ký tự.");
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (file.Length is <= 0 || file.Length > maxBytes || !MimeTypes.TryGetValue(ext, out var mimes) ||
                !mimes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase) || extensions is not null && !extensions.Contains(ext))
                throw new EvaluationRequestException("File rỗng, quá dung lượng hoặc extension/MIME không được phép.");
        }
    }
}
