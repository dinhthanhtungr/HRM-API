using DocumentFormat.OpenXml.Drawing.Diagrams;
using System;
using System.Collections.Generic;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Giá trị của một chỉ tiêu trong phiếu COA; cấu hình nằm tại phiên bản tiêu chuẩn.</summary>
public class CoaCertificateResult
{
    public Guid Id { get; set; }
    public Guid CoaCertificateId { get; set; }
    public Guid CoaSpecificationItemId { get; set; }
    /// <summary>Key đọc được của chỉ tiêu, ví dụ MOISTURE; lấy từ CriterionCode của standard item.</summary>
    public string ExternalId { get; set; } = string.Empty;
    public string? Value { get; set; }
    public CoaCertificate CoaCertificate { get; set; } = null!;
    public CoaSpecificationItem CoaSpecificationItem { get; set; } = null!;
}
