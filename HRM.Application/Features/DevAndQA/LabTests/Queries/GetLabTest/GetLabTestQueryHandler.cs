using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTest;

internal sealed class GetLabTestQueryHandler(LabTestService service)
    : IRequestHandler<GetLabTestQuery, LabTestDto>
{
    public Task<LabTestDto> Handle(GetLabTestQuery request, CancellationToken ct) => service.DetailAsync(request.Id, ct);
}
