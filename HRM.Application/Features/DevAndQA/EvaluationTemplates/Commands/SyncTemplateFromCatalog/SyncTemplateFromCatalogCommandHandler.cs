using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PatchEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.SyncTemplateFromCatalog;

internal sealed class SyncTemplateFromCatalogCommandHandler(EvaluationTemplateService service)
    : IRequestHandler<SyncTemplateFromCatalogCommand, EvaluationTemplateDto>
{
    public Task<EvaluationTemplateDto> Handle(SyncTemplateFromCatalogCommand request, CancellationToken ct)
    {
        if (request.Codes is null)
            throw new HRM.Application.Features.DevAndQA.Shared.Evaluations.EvaluationRequestException("codes không được null.");
        return service.PatchTemplateAsync(new PatchEvaluationTemplateCommand(request.Id, SyncStandardCodes: request.Codes), ct);
    }
}
