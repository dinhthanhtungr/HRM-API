using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTestLookup;

internal sealed class GetLabTestLookupQueryHandler(BasicLabTestService service)
    : IRequestHandler<GetLabTestLookupQuery, PagedResult<LabTestLookupDto>>
{
    public Task<PagedResult<LabTestLookupDto>> Handle(GetLabTestLookupQuery request, CancellationToken ct) =>
        service.LookupAsync(request, ct);
}
