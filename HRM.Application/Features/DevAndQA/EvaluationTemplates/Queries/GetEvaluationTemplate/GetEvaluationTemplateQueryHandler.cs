using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetEvaluationTemplate;

internal sealed class GetEvaluationTemplateQueryHandler(EvaluationTemplateService service) : IRequestHandler<GetEvaluationTemplateQuery, EvaluationTemplateDto>
{
    public Task<EvaluationTemplateDto> Handle(GetEvaluationTemplateQuery request, CancellationToken ct) => service.GetTemplateAsync(request.Id, ct);
}
