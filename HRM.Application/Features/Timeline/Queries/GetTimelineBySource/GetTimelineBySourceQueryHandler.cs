using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Timeline;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Timeline.Queries.GetTimelineBySource;

/// <summary>
/// Truy vấn EventLog active theo SourceId, áp dụng các bộ lọc được cung cấp và trả kết quả theo
/// thứ tự thời gian tăng dần để FE dựng timeline.
/// </summary>
internal sealed class GetTimelineBySourceQueryHandler
    : IRequestHandler<GetTimelineBySourceQuery, PagedResult<TimelineItemDto>>
{
    private readonly ITimelineDbContext _dbContext;

    public GetTimelineBySourceQueryHandler(ITimelineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<TimelineItemDto>> Handle(GetTimelineBySourceQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.EventLogs
            .AsNoTracking()
            .Where(x => x.IsActive && x.SourceId == request.SourceId);

        if (request.EventType.HasValue)
        {
            query = query.Where(x => x.EventType == request.EventType.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (request.CreatedBy is { } createdBy && createdBy != Guid.Empty)
        {
            query = query.Where(x => x.EmployeeID == createdBy);
        }

        if (request.From.HasValue)
        {
            query = query.Where(x => x.CreatedDate >= request.From.Value.Date);
        }

        if (request.To.HasValue)
        {
            var toExclusive = request.To.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedDate < toExclusive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.EventId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new TimelineItemDto
            {
                SourceId = x.SourceId,
                SourceCode = x.SourceCode,
                EventType = x.EventType,
                Status = x.Status,
                Note = x.Note,
                CreatedDate = x.CreatedDate,
                CreatedBy = x.EmployeeID,
                CreatedByName = x.CreatedByNavigation!.FullName,
                CompanyId = x.CompanyId,
                CompanyName = x.Company!.Name
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TimelineItemDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }
}
