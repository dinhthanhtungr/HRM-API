using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.ListBasicLabTests;

/// <summary>Phiếu test cơ bản theo company; không tạo Material hoặc voucher.</summary>
public sealed record ListBasicLabTestsQuery(int PageNumber = 1, int PageSize = 15) : IRequest<PagedResult<BasicLabTestDto>>;

