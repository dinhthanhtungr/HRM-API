using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.CreateEvaluationTemplate;

/// <summary>CreateEvaluationTemplate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record CreateEvaluationTemplateCommand(string Code, string Name, EvaluationTemplateType Type, int Version, IReadOnlyList<EvaluationTemplateItemRequest> Items) : IRequest<EvaluationTemplateDto>;
