using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;

/// <summary>Điều phối use case CreateEvaluationStandard qua service danh mục có scope và capability.</summary>
internal sealed class CreateEvaluationStandardCommandHandler(EvaluationStandardService service)
    : IRequestHandler<CreateEvaluationStandardCommand, OperationResult<EvaluationStandardDto>>
{
    public Task<OperationResult<EvaluationStandardDto>> Handle(CreateEvaluationStandardCommand request, CancellationToken ct) => service.CreateAsync(request, ct);
}
