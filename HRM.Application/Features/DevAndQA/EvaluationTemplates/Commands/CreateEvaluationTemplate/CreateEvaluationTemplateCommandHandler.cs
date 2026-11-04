using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.CreateEvaluationTemplate;

internal sealed class CreateEvaluationTemplateCommandHandler(EvaluationTemplateService service) : IRequestHandler<CreateEvaluationTemplateCommand, EvaluationTemplateDto>
{
    public Task<EvaluationTemplateDto> Handle(CreateEvaluationTemplateCommand request, CancellationToken ct) => service.CreateTemplateAsync(request, ct);
}
