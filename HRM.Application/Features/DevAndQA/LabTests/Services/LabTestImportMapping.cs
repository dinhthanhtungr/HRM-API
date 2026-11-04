using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.LabTests.Services;

/// <summary>Đối chiếu bố cục bảng test NVL; giữ quan sát phi số và thông tin ngoài cột có tên.</summary>
internal static partial class LabTestImportMapping
{
    public static string NormalizeHeader(string value) => new(value.Normalize(NormalizationForm.FormD)
        .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(x))
        .Select(char.ToLowerInvariant).ToArray());

    public static LabTestImportPreviewRowDto Map(LabTestSheet sheet, LabTestSheetRow row,
        IReadOnlyDictionary<string, EvaluationStandard> mapping)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var shift = sheet.Headers.Count > 1 && NormalizeHeader(sheet.Headers[1]) is "manvl" or "materialcode" ? 1 : 0;
        var density = 7 + shift;
        if (sheet.Headers.Count < 25 + shift || !sheet.Headers[density].StartsWith("Density", StringComparison.OrdinalIgnoreCase) ||
            !sheet.Headers[3 + shift].StartsWith("BATCH", StringComparison.OrdinalIgnoreCase) ||
            !sheet.Headers[6 + shift].Equals("RESIN", StringComparison.OrdinalIgnoreCase))
            throw new EvaluationRequestException("Bố cục chưa khớp mẫu TEST CƠ LÍ NVL; giữ thứ tự cột và thêm Mã NVL ngay sau TT nếu cần.");
        string? Cell(int index)
        {
            var value = row.Cells.ElementAtOrDefault(index + shift)?.Trim();
            return string.IsNullOrWhiteSpace(value) || value == "-" ? null : value;
        }
        if (shift == 1 && !string.IsNullOrWhiteSpace(row.Cells.ElementAtOrDefault(1)))
            warnings.Add("Mã NVL trong file chỉ tham khảo; import tạo LabTest với mã mới do backend sinh.");
        var dateText = Cell(1);
        var date = ParseDate(dateText);
        if (date is null) errors.Add($"Ngày test không hợp lệ: {dateText ?? "(trống)"}.");
        var results = new List<LabTestResultInput>();
        for (var i = 7; i <= 19; i++)
        {
            var raw = Cell(i);
            if (raw is null) continue;
            var header = sheet.Headers[i + shift];
            if (!mapping.TryGetValue(header, out var standard))
            { errors.Add($"Chưa ghép chỉ tiêu: {header}."); continue; }
            var config = new Shared.Evaluations.Dtos.EvaluationConfigurationDto(standard.DataType, standard.Unit,
                standard.MeasurementMethodId, null, standard.TestMethod, standard.InputConfigJson,
                standard.AcceptanceRuleJson, standard.SpecificationText, standard.IsRequired, standard.SortOrder);
            var value = raw;
            string? observed = null;
            if (standard.DataType is EvaluationDataType.Decimal or EvaluationDataType.Integer)
            {
                value = raw.Replace(',', '.');
                if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                {
                    observed = raw;
                    warnings.Add($"{header}: giữ dạng quan sát, chưa có giá trị số.");
                }
            }
            try
            {
                var normalized = EvaluationValueRules.Normalize(observed is null ? value : null, config);
                results.Add(new(standard.Id, normalized, ObservedText: observed));
            }
            catch (EvaluationRequestException ex)
            {
                errors.Add($"{header}: {ex.Message}");
                results.Add(new(standard.Id, value));
            }
        }
        var note = Cell(22);
        var weightText = Cell(24);
        decimal? weight = null;
        if (weightText is not null)
        {
            if (decimal.TryParse(weightText.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsed)) weight = parsed;
            else { note = Join(note, $"Khối lượng nguồn: {weightText}"); warnings.Add("Khối lượng dạng chữ được giữ trong Note."); }
        }
        for (var i = 25 + shift; i < row.Cells.Count; i++)
            if (!string.IsNullOrWhiteSpace(row.Cells[i]))
            {
                note = Join(note, $"{sheet.Headers[i]}: {row.Cells[i].Trim()}");
                warnings.Add($"Cột {i + 1} được giữ trong Note; cần kiểm tra vì không có tên nghiệp vụ.");
            }
        var photo = Cell(23)?.Replace('\\', '/').Split('/').Last();
        if (photo is not null) warnings.Add("Ảnh nguồn chỉ có tên/đường dẫn; file ảnh chưa được upload.");
        var data = new LabTestWriteRequest
        {
            SampleName = Cell(5), TestDate = date, SupplierCustomerName = Cell(2), BatchNumber = Cell(3),
            ItemCode = Cell(4), Resin = Cell(6), Application = Cell(20), PreparedByName = Cell(21),
            Note = note, ImportedPhotoName = photo, WeightKg = weight, Results = results
        };
        try { LabTestMetadataRules.Apply(new(), data, true); }
        catch (EvaluationRequestException ex) { errors.Add(ex.Message); }
        if (results.Select(x => x.EvaluationStandardId).Distinct().Count() != results.Count)
            errors.Add("Nhiều cột đang ghép vào cùng một chỉ tiêu.");
        return new(row.RowNumber, data, errors, warnings);
    }

    internal static DateOnly? ParseDate(string? value)
    {
        if (value is null) return null;
        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "d/M/yyyy", "d-M-yyyy", "d/M/yy", "d-M-yy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        var match = VietnameseDate().Match(value);
        if (!match.Success) return null;
        var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (year < 100) year += 2000;
        return DateOnly.TryParseExact($"{year:D4}-{int.Parse(match.Groups[2].Value):D2}-{int.Parse(match.Groups[1].Value):D2}",
            "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date : null;
    }

    private static string Join(string? before, string after) => before is null ? after : before + "\n" + after;

    [GeneratedRegex(@"^(\d{1,2})\s+Thg\s*(\d{1,2})\s+(\d{2}|\d{4})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VietnameseDate();
}
