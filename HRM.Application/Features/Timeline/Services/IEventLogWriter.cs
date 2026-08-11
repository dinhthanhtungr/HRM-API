using HRM.Application.Features.Timeline.Dtos;

namespace HRM.Application.Features.Timeline.Services;

/// <summary>
/// Ghi EventLog dùng chung cho các module nghiệp vụ. Service chỉ add entity vào DbContext,
/// caller chịu trách nhiệm SaveChanges/commit transaction.
/// </summary>
public interface IEventLogWriter
{
    Task AddAsync(EventLogCreateRequest request, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken cancellationToken = default);
}
