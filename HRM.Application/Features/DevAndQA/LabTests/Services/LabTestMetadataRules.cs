using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.LabTests.Services;

internal static class LabTestMetadataRules
{
    public static void Apply(LabTest entity, LabTestWriteRequest request, bool create)
    {
        var fields = new (string Key, string? Value, int Limit, Func<string?> Read, Action<string?> Write)[]
        {
            ("supplierCustomerName", request.SupplierCustomerName, 300, () => entity.SupplierCustomerName, v => entity.SupplierCustomerName = v),
            ("batchNumber", request.BatchNumber, 100, () => entity.BatchNumber, v => entity.BatchNumber = v),
            ("itemCode", request.ItemCode, 100, () => entity.ItemCode, v => entity.ItemCode = v),
            ("resin", request.Resin, 100, () => entity.Resin, v => entity.Resin = v),
            ("application", request.Application, 2000, () => entity.Application, v => entity.Application = v),
            ("preparedByName", request.PreparedByName, 200, () => entity.PreparedByName, v => entity.PreparedByName = v),
            ("note", request.Note, 16000, () => entity.Note, v => entity.Note = v),
            ("importedPhotoName", request.ImportedPhotoName, 255, () => entity.ImportedPhotoName, v => entity.ImportedPhotoName = v)
        };
        var clear = request.ClearFields ?? [];
        if (clear.Any(x => x is null || x != "weightKg" && !fields.Any(f => f.Key == x)) ||
            clear.Distinct().Count() != clear.Count || create && clear.Count > 0)
            throw new EvaluationRequestException("clearFields không hợp lệ; chỉ dùng cho PATCH field nullable.");
        if (create && (request.SampleName is null || request.TestDate is null))
            throw new EvaluationRequestException("SampleName và TestDate bắt buộc.");
        if (request.SampleName is { } name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
                throw new EvaluationRequestException("SampleName bắt buộc, tối đa 200 ký tự.");
            PatchHelper.Set(name.Trim(), () => entity.SampleName, v => entity.SampleName = v);
        }
        if (request.TestDate is { } date)
        {
            if (date == DateOnly.MinValue) throw new EvaluationRequestException("TestDate không hợp lệ.");
            PatchHelper.Set(date, () => entity.TestDate, v => entity.TestDate = v);
        }
        foreach (var field in fields)
        {
            if (clear.Contains(field.Key) && field.Value is not null)
                throw new EvaluationRequestException($"Không vừa gửi {field.Key} vừa clearFields.");
            if (field.Value?.Length > field.Limit)
                throw new EvaluationRequestException($"{field.Key} vượt {field.Limit} ký tự.");
            if (field.Key == "importedPhotoName" && field.Value is { } photo && (photo.Contains('/') || photo.Contains('\\')))
                throw new EvaluationRequestException("ImportedPhotoName chỉ nhận tên file, không nhận đường dẫn.");
            if (clear.Contains(field.Key) || field.Value is not null)
                PatchHelper.Set(clear.Contains(field.Key) || string.IsNullOrWhiteSpace(field.Value) ? null : field.Value.Trim(), field.Read, field.Write);
        }
        if (clear.Contains("weightKg") && request.WeightKg.HasValue || request.WeightKg is < 0 or > 999999999999.999999m ||
            request.WeightKg is { } weight && decimal.Round(weight, 6) != weight)
            throw new EvaluationRequestException("WeightKg không hợp lệ (không âm, tối đa 6 số thập phân) hoặc vừa gửi vừa xóa.");
        if (clear.Contains("weightKg") || request.WeightKg.HasValue)
            PatchHelper.Set(clear.Contains("weightKg") ? null : request.WeightKg, () => entity.WeightKg, v => entity.WeightKg = v);
    }
}
