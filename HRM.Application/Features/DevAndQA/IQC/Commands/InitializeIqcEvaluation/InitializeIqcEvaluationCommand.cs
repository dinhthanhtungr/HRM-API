using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.InitializeIqcEvaluation;

/// <summary>InitializeIqcEvaluation: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record InitializeIqcEvaluationCommand(long VoucherDetailId, Guid? EvaluationTemplateId = null) : IRequest<IqcEvaluationDto>;
