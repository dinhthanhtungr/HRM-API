using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Queries.GetCoaCertificate;

/// <summary>GetCoaCertificate: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetCoaCertificateQuery(Guid Id) : IRequest<CoaCertificateDto>;
