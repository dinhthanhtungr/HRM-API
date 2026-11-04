using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.RetireEvaluationTemplate;

internal sealed class RetireEvaluationTemplateCommandHandler(EvaluationTemplateService service) : IRequestHandler<RetireEvaluationTemplateCommand, EvaluationTemplateDto>
{
    public Task<EvaluationTemplateDto> Handle(RetireEvaluationTemplateCommand request, CancellationToken ct) => service.RetireTemplateAsync(request.Id, ct);
}
