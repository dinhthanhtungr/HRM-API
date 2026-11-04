using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.AddCoaCertificateResult;

/// <summary>AddCoaCertificateResult: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record AddCoaCertificateResultCommand(Guid CertificateId, Guid EvaluationStandardId, EvaluationConfigurationRequest? Configuration = null, string? Value = null) : IRequest<EvaluationResultDto>;
