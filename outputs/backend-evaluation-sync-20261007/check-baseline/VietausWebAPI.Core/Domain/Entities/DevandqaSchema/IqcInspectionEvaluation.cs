using DocumentFormat.OpenXml.Drawing.Diagrams;
using System;
using System.Collections.Generic;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
/// <summary>Liên kết phiếu IQC hiện có với phiên bản tiêu chuẩn dùng để nhập chỉ tiêu.</summary>
public class IqcInspectionEvaluation
{
    public Guid Id { get; set; }
    public Guid QCInputByQCId { get; set; }
    public Guid IqcSpecificationId { get; set; }
    public QCInputByQC QCInputByQC { get; set; } = null!;
    public IqcSpecification IqcSpecification { get; set; } = null!;
    public ICollection<IqcInspectionResult> Results { get; set; } = new List<IqcInspectionResult>();
}
