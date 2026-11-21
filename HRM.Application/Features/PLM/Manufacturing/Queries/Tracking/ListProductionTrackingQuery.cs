using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;

public sealed record ListProductionTrackingQuery(string? Keyword, int PageNumber)
    : IRequest<PagedResult<ProductionTrackingDto>?>;
