using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificate;

/// <summary>PatchCoaCertificate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record PatchCoaCertificateCommand(Guid Id, string? CertificateNo = null, string? BatchNo = null, DateOnly? ManufacturingDate = null, DateOnly? ExpiryDate = null, string? Notes = null, IReadOnlyList<string>? ClearFields = null) : IRequest<CoaCertificateDto>;
