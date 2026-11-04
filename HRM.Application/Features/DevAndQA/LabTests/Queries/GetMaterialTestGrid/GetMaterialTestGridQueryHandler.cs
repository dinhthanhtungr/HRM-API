using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.GetMaterialTestGrid;

internal sealed class GetMaterialTestGridQueryHandler(MaterialTestGridService service)
    : IRequestHandler<GetMaterialTestGridQuery, MaterialTestGridDto>
{
    public Task<MaterialTestGridDto> Handle(GetMaterialTestGridQuery request, CancellationToken ct) => service.ReadAsync(request, ct);
}
