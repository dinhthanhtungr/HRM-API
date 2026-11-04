using System.Text.Json;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;

public enum EvaluationFileResource { Iqc = 2, Coa = 3 }

/// <summary>Ghép file kết quả bằng EvaluationStandardId. FileKeys là tên part multipart.</summary>
public sealed record EvaluationFileBinding(Guid? EvaluationStandardId = null,
    IReadOnlyList<string>? FileKeys = null, IReadOnlyList<Guid>? DeleteAttachmentIds = null);
public sealed record EvaluationFileSaveEnvelope(JsonElement Payload, IReadOnlyList<EvaluationFileBinding>? Attachments = null);
public sealed record EvaluationNamedUpload(string Key, AttachmentUploadFile File);
public sealed record EvaluationFileGroupDto(Guid OwnerId, Guid? EvaluationStandardId, Guid? AttachmentCollectionId,
    IReadOnlyList<AttachmentDto> Files, EvaluationConfigurationDto? Configuration = null);
public sealed record EvaluationFileSaveDto(object Record, Guid ParentId, IReadOnlyList<EvaluationFileGroupDto> Attachments);

internal sealed record EvaluationFileOwner(Guid Id, Guid? StandardId, EvaluationConfigurationDto Configuration,
    Guid? CollectionId, Action<Guid?> SetCollection, bool IsResult);
