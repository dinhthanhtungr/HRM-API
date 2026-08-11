using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Timeline;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Entities.AuditSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Timeline.Services;

internal sealed class EventLogWriter : IEventLogWriter
{
    private readonly ITimelineDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public EventLogWriter(ITimelineDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task AddAsync(EventLogCreateRequest request, CancellationToken cancellationToken = default)
    {
        return AddRangeAsync(new[] { request }, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken cancellationToken = default)
    {
        var items = requests.Where(x => x is not null).ToList();
        if (items.Count == 0)
        {
            return;
        }

        if (items.Any(x => x.EmployeeId == Guid.Empty || x.SourceId == Guid.Empty))
        {
            throw new InvalidOperationException("EmployeeId và SourceId là bắt buộc khi ghi EventLog.");
        }

        var employeeIds = items
            .Where(x => !x.CompanyId.HasValue || !x.DepartmentId.HasValue)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToArray();

        var employees = employeeIds.Length == 0
            ? new Dictionary<Guid, EmployeeContext>()
            : await _dbContext.Employees
                .AsNoTracking()
                .Where(x => employeeIds.Contains(x.EmployeeId))
                .Select(x => new EmployeeContext(x.EmployeeId, x.CompanyId, x.PartId))
                .ToDictionaryAsync(x => x.EmployeeId, cancellationToken);

        var now = _dateTimeProvider.Now;
        var logs = new List<EventLog>(items.Count);
        foreach (var item in items)
        {
            employees.TryGetValue(item.EmployeeId, out var employee);
            var companyId = item.CompanyId ?? employee?.CompanyId;
            if (!companyId.HasValue || companyId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("Không tìm thấy công ty của nhân viên ghi log.");
            }

            var departmentId = item.DepartmentId ?? employee?.DepartmentId;
            if (!departmentId.HasValue || departmentId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("Không tìm thấy phòng ban của nhân viên ghi log.");
            }

            logs.Add(new EventLog
            {
                EventId = Guid.CreateVersion7(),
                CompanyId = companyId.Value,
                DepartmentId = departmentId.Value,
                SourceId = item.SourceId,
                SourceCode = string.IsNullOrWhiteSpace(item.SourceCode) ? string.Empty : item.SourceCode.Trim(),
                EventType = item.EventType,
                Status = string.IsNullOrWhiteSpace(item.Status) ? string.Empty : item.Status.Trim(),
                IsActive = true,
                Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
                EmployeeID = item.EmployeeId,
                CreatedDate = item.CreatedDate ?? now
            });
        }

        await _dbContext.EventLogs.AddRangeAsync(logs, cancellationToken);
    }

    private sealed record EmployeeContext(Guid EmployeeId, Guid? CompanyId, Guid? DepartmentId);
}
