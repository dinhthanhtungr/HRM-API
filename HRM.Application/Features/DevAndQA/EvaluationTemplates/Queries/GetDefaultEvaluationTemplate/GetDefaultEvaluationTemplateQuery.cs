using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetDefaultEvaluationTemplate;

/// <summary>GetDefaultEvaluationTemplate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetDefaultEvaluationTemplateQuery(EvaluationTemplateType Type) : IRequest<EvaluationTemplateDto>;
