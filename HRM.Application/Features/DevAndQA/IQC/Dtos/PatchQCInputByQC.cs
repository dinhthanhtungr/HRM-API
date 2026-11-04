using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public class PatchQCInputByQC
{
    public IqcEvaluationSaveRequest? Evaluation { get; set; }
    public IReadOnlyList<string> ClearFields { get; set; } = Array.Empty<string>();
    public string? InspectionMethod { get; set; }
    public bool? IsCOAProvided { get; set; }
    public bool? IsMSDSTDSProvided { get; set; }
    public bool? IsMetalDetectionRequired { get; set; }
    public bool? IsSuccessQuality { get; set; }
    public QcDecision? ImportWarehouseType { get; set; }
    public string? Note { get; set; }

    // nếu muốn cho đổi attachment collection
    public Guid? AttachmentCollectionId { get; set; }
    public bool? HasNewAttachments { get; set; }
}
