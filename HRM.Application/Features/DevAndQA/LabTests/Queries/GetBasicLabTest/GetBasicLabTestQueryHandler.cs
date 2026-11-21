using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.GetBasicLabTest;

internal sealed class GetBasicLabTestQueryHandler(BasicLabTestService service) : IRequestHandler<GetBasicLabTestQuery, BasicLabTestDto>
{
    public Task<BasicLabTestDto> Handle(GetBasicLabTestQuery request, CancellationToken ct) => service.DetailAsync(request.Id, ct);
}

