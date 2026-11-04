using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CreateCoaCertificate;

/// <summary>CreateCoaCertificate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record CreateCoaCertificateCommand(Guid ProductId, string CertificateNo, string BatchNo, DateOnly? ManufacturingDate = null, DateOnly? ExpiryDate = null, string? Notes = null, Guid? EvaluationTemplateId = null) : IRequest<CoaCertificateDto>;
