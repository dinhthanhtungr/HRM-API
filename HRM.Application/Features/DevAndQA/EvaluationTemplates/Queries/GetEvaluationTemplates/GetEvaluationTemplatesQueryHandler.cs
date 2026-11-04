using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetEvaluationTemplates;

internal sealed class GetEvaluationTemplatesQueryHandler(EvaluationTemplateService service) : IRequestHandler<GetEvaluationTemplatesQuery, PagedResult<EvaluationTemplateDto>>
{
    public Task<PagedResult<EvaluationTemplateDto>> Handle(GetEvaluationTemplatesQuery request, CancellationToken ct) => service.ListTemplatesAsync(request, ct);
}
