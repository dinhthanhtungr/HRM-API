using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;

/// <summary>Kết quả bổ sung tùy chọn lưu cùng phiếu QC; bỏ/null/Results=[] giữ dữ liệu đánh giá hiện có.</summary>
public sealed record IqcEvaluationSaveRequest(
    Guid? EvaluationTemplateId = null,
    IReadOnlyList<IqcEvaluationSaveRow>? Results = null);

/// <summary>Dòng preview dùng SourceTemplateItemId/StandardId; dòng đã lưu dùng ResultId, null Value giữ nguyên.</summary>
public sealed record IqcEvaluationSaveRow(
    Guid EvaluationStandardId,
    Guid? ResultId = null,
    Guid? SourceTemplateItemId = null,
    string? Value = null,
    bool ClearValue = false,
    EvaluationConfigurationRequest? Configuration = null);
