using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetEvaluationTemplate;

/// <summary>GetEvaluationTemplate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetEvaluationTemplateQuery(Guid Id) : IRequest<EvaluationTemplateDto>;
