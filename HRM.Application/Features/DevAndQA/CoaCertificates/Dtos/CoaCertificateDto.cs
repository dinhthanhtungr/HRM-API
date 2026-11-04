using HRM.Domain.Enums.Devandqa;

using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;

public sealed record CoaCertificateDto(Guid Id, string CertificateNo, string ProductCode, string ProductName,
    string BatchNo, DateOnly? ManufacturingDate, DateOnly? ExpiryDate, CoaCertificateStatus Status,
    string? Notes, Guid? EvaluationTemplateId, string? TemplateCode, string? TemplateName, int? TemplateVersion,
    Guid CreatedBy, DateTime CreatedDate, Guid? IssuedBy, DateTime? IssuedDate,
    IReadOnlyList<EvaluationResultDto> Results);
