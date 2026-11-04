using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluation;

/// <summary>GetIqcEvaluation: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetIqcEvaluationQuery(long VoucherDetailId) : IRequest<IqcEvaluationDto>;
