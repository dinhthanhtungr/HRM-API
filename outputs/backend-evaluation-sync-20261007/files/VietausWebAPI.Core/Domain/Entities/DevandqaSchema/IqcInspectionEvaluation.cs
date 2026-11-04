namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Đầu đánh giá của phiếu IQC; lưu nguồn template chung hoặc bộ tiêu chuẩn cũ.</summary>
public class IqcInspectionEvaluation
{
    public Guid Id { get; set; }
    public Guid? EvaluationTemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public string? TemplateName { get; set; }
    public int? TemplateVersion { get; set; }
    public EvaluationTemplate? EvaluationTemplate { get; set; }
    public Guid QCInputByQCId { get; set; }
    public Guid? IqcSpecificationId { get; set; }
    public QCInputByQC QCInputByQC { get; set; } = null!;
    public IqcSpecification? IqcSpecification { get; set; }
    public ICollection<IqcInspectionResult> Results { get; set; } = new List<IqcInspectionResult>();
}
