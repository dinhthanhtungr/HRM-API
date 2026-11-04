using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;

/// <summary>Điều phối use case PatchEvaluationStandard qua service danh mục có scope và capability.</summary>
internal sealed class PatchEvaluationStandardCommandHandler(EvaluationStandardService service)
    : IRequestHandler<PatchEvaluationStandardCommand, OperationResult<EvaluationStandardDto>>
{
    public Task<OperationResult<EvaluationStandardDto>> Handle(PatchEvaluationStandardCommand request, CancellationToken ct) => service.PatchAsync(request, ct);
}
