using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificateResult;

/// <summary>PatchCoaCertificateResult: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record PatchCoaCertificateResultCommand(Guid CertificateId, Guid ResultId, string? Value = null, bool ClearValue = false, EvaluationConfigurationRequest? Configuration = null) : IRequest<EvaluationResultDto>;
