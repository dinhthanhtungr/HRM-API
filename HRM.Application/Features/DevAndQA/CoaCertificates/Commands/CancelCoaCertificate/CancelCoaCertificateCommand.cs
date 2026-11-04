using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CancelCoaCertificate;

/// <summary>CancelCoaCertificate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record CancelCoaCertificateCommand(Guid Id) : IRequest<CoaCertificateDto>;
