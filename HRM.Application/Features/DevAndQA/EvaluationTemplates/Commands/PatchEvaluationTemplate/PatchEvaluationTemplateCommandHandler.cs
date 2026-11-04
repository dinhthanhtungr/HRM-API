using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PatchEvaluationTemplate;

internal sealed class PatchEvaluationTemplateCommandHandler(EvaluationTemplateService service) : IRequestHandler<PatchEvaluationTemplateCommand, EvaluationTemplateDto>
{
    public Task<EvaluationTemplateDto> Handle(PatchEvaluationTemplateCommand request, CancellationToken ct) => service.PatchTemplateAsync(request, ct);
}
