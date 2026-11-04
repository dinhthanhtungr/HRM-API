using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public class PostQCInputByQC
{
    public IqcEvaluationSaveRequest? Evaluation { get; set; }
    public long VoucherDetailId { get; set; }

    public string? CSName { get; set; }
    public string? CSExternalId { get; set; }

    public string? MaterialName { get; set; }
    public string? MaterialExternalId { get; set; }

    public string? InspectionMethod { get; set; }
    public bool? IsCOAProvided { get; set; }
    public bool? IsMSDSTDSProvided { get; set; }
    public bool? IsMetalDetectionRequired { get; set; }
    public bool? IsSuccessQuality { get; set; } // Kiểm tra chất lượng đạt

    public QcDecision? ImportWarehouseType { get; set; }
    public string? Note { get; set; }

    public Guid? AttachmentCollectionId { get; set; }
    public bool? HasNewAttachments { get; set; }
}
