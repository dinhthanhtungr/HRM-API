using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandardDetail;

/// <summary>Đọc một tiêu chuẩn trong company; không tiết lộ record công ty khác.</summary>
public sealed record GetEvaluationStandardDetailQuery(Guid Id) : IRequest<OperationResult<EvaluationStandardDto?>>;
