using DocumentFormat.OpenXml.Drawing.Diagrams;
using System;
using System.Collections.Generic;
using VietausWebAPI.Core.Domain.Enums.Devandqa;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Bộ tiêu chuẩn IQC độc lập; mỗi bản ghi đại diện một phiên bản.</summary>
public class IqcSpecification
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid MaterialId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public IqcSpecificationStatus Status { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public ICollection<IqcSpecificationItem> Items { get; set; } = new List<IqcSpecificationItem>();
}
