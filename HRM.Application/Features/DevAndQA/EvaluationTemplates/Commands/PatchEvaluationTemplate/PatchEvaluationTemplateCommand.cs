using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PatchEvaluationTemplate;

/// <summary>PatchEvaluationTemplate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record PatchEvaluationTemplateCommand(Guid Id, string? Code = null, string? Name = null,
    IReadOnlyList<EvaluationTemplateItemRequest>? Items = null,
    IReadOnlyList<string>? SyncStandardCodes = null) : IRequest<EvaluationTemplateDto>;
