using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>
/// Kiểm tra và áp dụng PATCH thuần: field bỏ qua giữ nguyên, xóa qua clearFields có whitelist và kiểm tra conflict.
/// </summary>
/// 
internal static class IqcMutation
{
    private static readonly IReadOnlyDictionary<string, Action<QCInputByQC>> Clearers =
        new Dictionary<string, Action<QCInputByQC>>(StringComparer.Ordinal)
        {
            ["inspectionMethod"] = x => x.InspectionMethod = null,
            ["isCOAProvided"] = x => x.IsCOAProvided = null,
            ["isMSDSTDSProvided"] = x => x.IsMSDSTDSProvided = null,
            ["isMetalDetectionRequired"] = x => x.IsMetalDetectionRequired = null,
            ["isSuccessQuality"] = x => x.IsSuccessQuality = null,
            ["importWarehouseType"] = x => x.ImportWarehouseType = null,
            ["note"] = x => x.Note = null
        };

    public static string? Validate(PatchQCInputByQC request)
    {
        var error = IqcRules.ValidateDecision(request.ImportWarehouseType);
        if (error is not null) return error;
        if (request.ClearFields is null) return "clearFields không được null.";
        foreach (var field in request.ClearFields)
        {
            if (field is null || !Clearers.ContainsKey(field)) return "clearFields chứa field không hợp lệ.";
            var hasValue = field switch
            {
                "inspectionMethod" => request.InspectionMethod is not null,
                "isCOAProvided" => request.IsCOAProvided.HasValue,
                "isMSDSTDSProvided" => request.IsMSDSTDSProvided.HasValue,
                "isMetalDetectionRequired" => request.IsMetalDetectionRequired.HasValue,
                "isSuccessQuality" => request.IsSuccessQuality.HasValue,
                "importWarehouseType" => request.ImportWarehouseType.HasValue,
                "note" => request.Note is not null,
                _ => false
            };
            if (hasValue) return $"Không được vừa gửi value vừa clear {field}.";
        }
        return null;
    }

    public static bool Apply(QCInputByQC entity, PatchQCInputByQC request)
    {
        var changed = PatchHelper.SetIfNotNull(request.InspectionMethod, () => entity.InspectionMethod, x => entity.InspectionMethod = x);
        if (request.IsCOAProvided.HasValue)
            changed |= PatchHelper.SetNullable(request.IsCOAProvided, () => entity.IsCOAProvided, x => entity.IsCOAProvided = x);
        if (request.IsMSDSTDSProvided.HasValue)
            changed |= PatchHelper.SetNullable(request.IsMSDSTDSProvided, () => entity.IsMSDSTDSProvided, x => entity.IsMSDSTDSProvided = x);
        if (request.IsMetalDetectionRequired.HasValue)
            changed |= PatchHelper.SetNullable(request.IsMetalDetectionRequired, () => entity.IsMetalDetectionRequired, x => entity.IsMetalDetectionRequired = x);
        if (request.IsSuccessQuality.HasValue)
            changed |= PatchHelper.SetNullable(request.IsSuccessQuality, () => entity.IsSuccessQuality, x => entity.IsSuccessQuality = x);
        if (request.ImportWarehouseType.HasValue)
            changed |= PatchHelper.SetNullable(request.ImportWarehouseType, () => entity.ImportWarehouseType, x => entity.ImportWarehouseType = x);
        changed |= PatchHelper.SetIfNotNull(request.Note, () => entity.Note, x => entity.Note = x);
        foreach (var field in request.ClearFields)
        {
            Clearers[field](entity);
            changed = true;
        }
        return changed;
    }
}
