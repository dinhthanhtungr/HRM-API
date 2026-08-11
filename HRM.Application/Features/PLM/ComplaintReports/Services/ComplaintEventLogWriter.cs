using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Logs;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

internal sealed class ComplaintEventLogWriter
{
    private readonly IEventLogWriter _eventLogWriter;

    public ComplaintEventLogWriter(IEventLogWriter eventLogWriter)
    {
        _eventLogWriter = eventLogWriter;
    }

    public Task AddAsync(
        ComplaintReport report,
        Guid employeeId,
        string note,
        CancellationToken cancellationToken)
        => _eventLogWriter.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId,
            SourceId = report.ComplaintReportId,
            SourceCode = report.ExternalId,
            EventType = EventType.ComplaintReport,
            Status = report.Status.ToString(),
            Note = note,
            CompanyId = report.CompanyId,
            SourceType = nameof(ComplaintReport)
        }, cancellationToken);
}
