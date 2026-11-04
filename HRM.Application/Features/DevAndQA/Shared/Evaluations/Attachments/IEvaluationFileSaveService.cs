using HRM.Application.Features.Attachments.Dtos;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;

public interface IEvaluationFileSaveService
{
    Task<EvaluationFileSaveDto> SaveAsync(EvaluationFileResource resource, string? parentKey, bool create,
        EvaluationFileSaveEnvelope request, IReadOnlyList<EvaluationNamedUpload> uploads, CancellationToken ct);
    Task<IReadOnlyList<EvaluationFileGroupDto>> ListAsync(EvaluationFileResource resource, Guid parentId, CancellationToken ct);
    Task<AttachmentContent> ContentAsync(EvaluationFileResource resource, Guid parentId, Guid ownerId, Guid attachmentId, CancellationToken ct);
}
