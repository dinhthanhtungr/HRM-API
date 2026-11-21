using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.ListBasicLabTests;

internal sealed class ListBasicLabTestsQueryHandler(BasicLabTestService service) : IRequestHandler<ListBasicLabTestsQuery, PagedResult<BasicLabTestDto>>
{
    public Task<PagedResult<BasicLabTestDto>> Handle(ListBasicLabTestsQuery request, CancellationToken ct) => service.ListAsync(request.PageNumber, request.PageSize, ct);
}

