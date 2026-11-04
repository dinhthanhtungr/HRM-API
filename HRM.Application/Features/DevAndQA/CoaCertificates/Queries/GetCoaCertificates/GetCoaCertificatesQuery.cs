using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Queries.GetCoaCertificates;

/// <summary>GetCoaCertificates: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetCoaCertificatesQuery(string? Keyword = null, int PageNumber = 1, int PageSize = 50) : IRequest<PagedResult<CoaCertificateDto>>;
