using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Queries.GetCoaCertificate;

internal sealed class GetCoaCertificateQueryHandler(CoaCertificateService service) : IRequestHandler<GetCoaCertificateQuery, CoaCertificateDto>
{
    public Task<CoaCertificateDto> Handle(GetCoaCertificateQuery request, CancellationToken ct) => service.GetCoaAsync(request.Id, ct);
}
