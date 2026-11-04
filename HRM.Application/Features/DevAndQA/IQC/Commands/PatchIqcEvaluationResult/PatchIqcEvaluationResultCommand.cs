using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqcEvaluationResult;

/// <summary>PatchIqcEvaluationResult: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record PatchIqcEvaluationResultCommand(long VoucherDetailId, Guid ResultId, string? Value = null, bool ClearValue = false, EvaluationConfigurationRequest? Configuration = null) : IRequest<EvaluationResultDto>;
