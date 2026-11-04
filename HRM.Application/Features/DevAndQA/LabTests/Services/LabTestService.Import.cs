using System.Security.Cryptography;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.LabTests.Services;

internal sealed partial class LabTestService
{
    /// <summary>Preview không ghi DB; chỉ ghép đúng tên hoặc mapping cột được chọn, không tạo danh mục.</summary>
    public async Task<LabTestImportPreviewDto> PreviewImportAsync(byte[] content, string fileName,
        IReadOnlyDictionary<string, Guid>? columnStandards, ILabTestSheetReader reader, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        await access.EmployeeAsync(company, ct);
        var sheet = reader.Read(content, fileName);
        var standards = await db.EvaluationStandards.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive).ToListAsync(ct);
        if (columnStandards is { Count: > 100 } || columnStandards?.Any(x => !sheet.Headers.Contains(x.Key, StringComparer.Ordinal) ||
            !standards.Any(s => s.Id == x.Value)) == true)
            throw new EvaluationRequestException("Mapping chứa cột/chỉ tiêu không hợp lệ hoặc ngoài company.");
        var mapping = new Dictionary<string, EvaluationStandard>(StringComparer.Ordinal);
        foreach (var header in sheet.Headers)
        {
            if (columnStandards?.TryGetValue(header, out var explicitId) == true)
            { mapping[header] = standards.Single(x => x.Id == explicitId); continue; }
            var name = LabTestImportMapping.NormalizeHeader(header.Split('(')[0]);
            var matches = standards.Where(x => new[] { x.Code, x.Name, x.NameEn }.Any(v =>
                v is not null && LabTestImportMapping.NormalizeHeader(v) == name)).ToArray();
            if (matches.Length == 1) mapping[header] = matches[0];
        }
        var batchId = new Guid(SHA256.HashData(content).AsSpan(0, 16));
        var rows = new List<LabTestImportPreviewRowDto>();
        foreach (var row in sheet.Rows)
        {
            ct.ThrowIfCancellationRequested();
            rows.Add(LabTestImportMapping.Map(sheet, row, mapping));
        }
        return new(batchId, rows.Count, sheet.IgnoredRows, sheet.Headers, sheet.Warnings, rows,
            mapping.ToDictionary(x => x.Key, x => x.Value.Id));
    }

    /// <summary>Mỗi lần commit tối đa 500 phiếu, atomic và chống lặp theo company/batch/dòng; không cập nhật IQC.</summary>
    public async Task<LabTestImportResultDto> ImportAsync(Guid batchId, IReadOnlyList<LabTestImportRow> rows, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        var employee = await access.EmployeeAsync(company, ct);
        if (batchId == Guid.Empty || rows is null || rows.Count is < 1 or > 500 ||
            rows.Any(x => x is null || x.RowNumber is < 1 or > 10000 || x.Data is null) ||
            rows.Select(x => x.RowNumber).Distinct().Count() != rows.Count)
            throw new EvaluationRequestException("ImportBatchId/dòng không hợp lệ; mỗi commit từ 1 đến 500 dòng, không trùng.");
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var rowNumbers = rows.Select(x => x.RowNumber).ToArray();
        // Kể cả đã xóa mềm: retry import không làm sống lại dữ liệu đã xóa.
        var existing = await db.LabTests.Where(x => x.CompanyId == company && x.ImportBatchId == batchId &&
            x.ImportRowNumber.HasValue && rowNumbers.Contains(x.ImportRowNumber.Value))
            .Select(x => new { x.Id, x.Code, RowNumber = x.ImportRowNumber!.Value }).ToDictionaryAsync(x => x.RowNumber, ct);
        if (existing.Count == rows.Count)
        {
            await transaction.CommitAsync(ct);
            return new(0, existing.Count, rows.Select(x => new LabTestImportCreatedDto(x.RowNumber,
                existing[x.RowNumber].Id, existing[x.RowNumber].Code, true)).ToList());
        }
        var newRows = rows.Where(x => !existing.ContainsKey(x.RowNumber)).ToArray();
        if (newRows.Any(x => x.Data.Results is { Count: > 200 } || x.Data.Results?.Any(r => r is null) == true))
            throw new EvaluationRequestException("Danh sách chỉ tiêu chứa dòng null hoặc vượt 200 chỉ tiêu/phiếu.");
        var standardIds = newRows.SelectMany(x => x.Data.Results ?? [])
            .Select(x => x.EvaluationStandardId).Distinct().ToArray();
        if (standardIds.Length > 2000) throw new EvaluationRequestException("Mỗi commit tối đa 2000 chỉ tiêu khác nhau.");
        var catalog = await CatalogSnapshotsAsync(standardIds, company, ct);
        var nextCode = await NextCodeAsync(company, ct);
        var sequence = long.Parse(nextCode[CodePrefix.Length..], System.Globalization.CultureInfo.InvariantCulture);
        var staged = new List<LabTest>();
        var items = new List<LabTestImportCreatedDto>();
        foreach (var input in rows)
        {
            if (existing.TryGetValue(input.RowNumber, out var old))
            { items.Add(new(input.RowNumber, old.Id, old.Code, true)); continue; }
            if (sequence > 99999999) throw new EvaluationRequestException("Đã hết dải mã phiếu test.", 409);
            try
            {
                var entity = await StageCreateAsync(input.Data, company, employee,
                    CodePrefix + (sequence++).ToString("D8", System.Globalization.CultureInfo.InvariantCulture), ct, catalog);
                entity.ImportBatchId = batchId; entity.ImportRowNumber = input.RowNumber;
                staged.Add(entity);
                items.Add(new(input.RowNumber, entity.Id, entity.Code, false));
            }
            catch (EvaluationRequestException ex)
            { throw new EvaluationRequestException($"Dòng {input.RowNumber}: {ex.Message}", ex.StatusCode, ex); }
        }
        foreach (var entity in staged) db.LabTests.Add(entity);
        if (staged.Count > 0) await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return new(staged.Count, existing.Count, items);
    }
}
