using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Queries.GetCoaCertificates;

internal sealed class GetCoaCertificatesQueryHandler(CoaCertificateService service) : IRequestHandler<GetCoaCertificatesQuery, PagedResult<CoaCertificateDto>>
{
    public Task<PagedResult<CoaCertificateDto>> Handle(GetCoaCertificatesQuery request, CancellationToken ct) => service.ListCoaAsync(request.Keyword, request.PageNumber, request.PageSize, ct);
}
