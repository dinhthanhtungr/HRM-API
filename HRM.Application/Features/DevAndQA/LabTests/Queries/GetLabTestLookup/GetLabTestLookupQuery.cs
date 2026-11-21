using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTestLookup;

/// <summary>Tìm NVL, NCC hoặc nhân viên LabUser đang hoạt động trong company hiện tại.</summary>
public sealed record GetLabTestLookupQuery(LabTestLookupKind Kind, string? Keyword = null,
    int PageNumber = 1, int PageSize = 20, Guid? SelectedId = null) : IRequest<PagedResult<LabTestLookupDto>>;
