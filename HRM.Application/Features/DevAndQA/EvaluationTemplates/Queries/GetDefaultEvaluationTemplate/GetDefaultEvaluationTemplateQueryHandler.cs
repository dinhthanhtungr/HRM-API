using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetDefaultEvaluationTemplate;

internal sealed class GetDefaultEvaluationTemplateQueryHandler(EvaluationTemplateService service) : IRequestHandler<GetDefaultEvaluationTemplateQuery, EvaluationTemplateDto>
{
    public Task<EvaluationTemplateDto> Handle(GetDefaultEvaluationTemplateQuery request, CancellationToken ct) => service.GetDefaultTemplateAsync(request.Type, ct);
}
