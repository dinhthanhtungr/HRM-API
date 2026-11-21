using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.LabTests.Dtos;

/// <summary>Phiếu test tối giản; thông tin mở rộng do Lab cấu hình bằng chỉ tiêu động, không phải cột Material.</summary>
public sealed record BasicLabTestDto(Guid Id, string Code, string SampleName, DateOnly TestDate, string? Note,
    Guid? EvaluationTemplateId, string? TemplateCode, string? TemplateName, int? TemplateVersion,
    DateTime CreatedDate, Guid CreatedBy, IReadOnlyList<EvaluationResultDto> Results,
    string? BatchNumber = null, decimal? WeightKg = null, Guid? SupplierId = null, string? SupplierName = null,
    string? CreatedByName = null, Guid? MaterialId = null, string? MaterialCode = null);

/// <summary>Ngày bắt buộc; tên nhập tay hoặc snapshot từ MaterialId. PATCH bỏ/null giữ, xóa bằng clearFields. Results chỉ sửa dòng gửi lên.</summary>
public sealed record BasicLabTestWriteRequest(string? SampleName = null, DateOnly? TestDate = null, string? Note = null,
    Guid? EvaluationTemplateId = null, IReadOnlyList<LabTestResultInput>? Results = null,
    IReadOnlyList<string>? ClearFields = null, string? BatchNumber = null, decimal? WeightKg = null, Guid? SupplierId = null,
    Guid? CreatedBy = null, Guid? MaterialId = null);
