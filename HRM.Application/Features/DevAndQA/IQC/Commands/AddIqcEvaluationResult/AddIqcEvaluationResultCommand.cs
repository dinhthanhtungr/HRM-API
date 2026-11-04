using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.AddIqcEvaluationResult;

/// <summary>AddIqcEvaluationResult: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record AddIqcEvaluationResultCommand(long VoucherDetailId, Guid EvaluationStandardId, EvaluationConfigurationRequest? Configuration = null, string? Value = null) : IRequest<EvaluationResultDto>;
