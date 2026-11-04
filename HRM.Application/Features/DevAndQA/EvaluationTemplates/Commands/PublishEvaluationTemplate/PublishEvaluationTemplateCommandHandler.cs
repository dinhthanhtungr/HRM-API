using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PublishEvaluationTemplate;

internal sealed class PublishEvaluationTemplateCommandHandler(EvaluationTemplateService service) : IRequestHandler<PublishEvaluationTemplateCommand, EvaluationTemplateDto>
{
    public Task<EvaluationTemplateDto> Handle(PublishEvaluationTemplateCommand request, CancellationToken ct) => service.PublishTemplateAsync(request, ct);
}
