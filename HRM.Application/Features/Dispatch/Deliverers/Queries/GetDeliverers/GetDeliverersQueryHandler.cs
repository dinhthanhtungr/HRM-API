using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.Deliverers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.Deliverers.Queries.GetDeliverers;

internal sealed class GetDeliverersQueryHandler
    : IRequestHandler<GetDeliverersQuery, PagedResult<DelivererDto>>
{
    private readonly IDispatchReadDbContext _dbContext;

    public GetDeliverersQueryHandler(IDispatchReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<DelivererDto>> Handle(
        GetDeliverersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DelivererInfors
            .AsNoTracking()
            .AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                (x.Phone ?? string.Empty).Contains(keyword) ||
                (x.DelivererType ?? string.Empty).Contains(keyword));
        }

        var projected = query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new DelivererDto
            {
                Id = x.Id,
                Name = x.Name,
                DelivererType = x.DelivererType,
                Phone = x.Phone,
                Note = x.Note,
                IsActive = x.IsActive
            });

        return await projected.ToPagedResultAsync(
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            cancellationToken);
    }
}
