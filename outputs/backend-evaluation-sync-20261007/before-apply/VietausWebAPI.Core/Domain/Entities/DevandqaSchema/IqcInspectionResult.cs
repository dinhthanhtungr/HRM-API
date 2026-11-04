using DocumentFormat.OpenXml.Drawing.Diagrams;
using System;
using System.Collections.Generic;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Giá trị của một chỉ tiêu trong phiếu IQC; cấu hình nằm tại phiên bản tiêu chuẩn.</summary>
public class IqcInspectionResult
{
    public Guid Id { get; set; }
    public Guid IqcInspectionEvaluationId { get; set; }
    public Guid IqcSpecificationItemId { get; set; }
    /// <summary>Key đọc được của chỉ tiêu, ví dụ MOISTURE; lấy từ CriterionCode của standard item.</summary>
    public string ExternalId { get; set; } = string.Empty;
    public string? Value { get; set; }
    public IqcInspectionEvaluation IqcInspectionEvaluation { get; set; } = null!;
    public IqcSpecificationItem IqcSpecificationItem { get; set; } = null!;
}
