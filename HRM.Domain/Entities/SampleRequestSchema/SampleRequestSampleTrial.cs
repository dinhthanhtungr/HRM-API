using HRM.Domain.Enums.SampleRequests;

namespace HRM.Domain.Entities.SampleRequestSchema;

public partial class SampleRequestSampleTrial
{
    public Guid SampleRequestSampleTrialId { get; set; }
    public Guid SampleRequestId { get; set; }
    public Guid? FormulaId { get; set; }
    public int TrialNo { get; set; }
    public SampleTrialStatus Status { get; set; }

    public string? CustomerNameSnapshot { get; set; }
    public string? SampleRequestExternalIdSnapshot { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ColourCodeSnapshot { get; set; }
    public string? CategoryNameSnapshot { get; set; }

    public string? BatchNo { get; set; }
    public decimal? DeliveredSampleQuantityKg { get; set; }
    public decimal? AdditiveRate { get; set; }
    public DateTime? RequestReceivedDate { get; set; }
    public DateTime? FinishedDate { get; set; }
    public DateTime? SentDate { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? LabNote { get; set; }
    public Guid? SentByEmployeeId { get; set; }

    public string? CustomerReplyStatus { get; set; }
    public DateTime? CustomerReplyDate { get; set; }
    public Guid? CustomerReplyByEmployeeId { get; set; }
    public string? CustomerReplyNote { get; set; }
    public DateTime? OrderDate { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual SampleRequest SampleRequest { get; set; } = null!;
    public virtual Formula? Formula { get; set; }
}
