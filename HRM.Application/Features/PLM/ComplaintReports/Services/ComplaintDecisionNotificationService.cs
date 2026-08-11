using System.Text.Json;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

internal sealed class ComplaintDecisionNotificationService
{
    private readonly INotificationService _notificationService;

    public ComplaintDecisionNotificationService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task PublishInitialAsync(
        ComplaintReport report,
        ComplaintApprovalDecision decision,
        Guid actorId,
        Guid? handlingOrderId,
        CancellationToken cancellationToken)
        => PublishAsync(
            report,
            TopicNotifications.ComplaintInitialDecision,
            $"Khiếu nại {report.ExternalId} đã có quyết định ban đầu",
            $"Quyết định: {decision}. Hướng xử lý: {report.ResolutionType?.ToString() ?? "None"}.",
            actorId,
            new Guid?[] { report.CreatedBy },
            new { report.ComplaintReportId, report.Status, report.ResolutionType, Decision = decision, HandlingOrderId = handlingOrderId },
            cancellationToken);

    public Task PublishFinalAsync(
        ComplaintReport report,
        ComplaintApprovalDecision decision,
        Guid actorId,
        IEnumerable<Guid?> participantIds,
        CancellationToken cancellationToken)
        => PublishAsync(
            report,
            TopicNotifications.ComplaintFinalDecision,
            $"Khiếu nại {report.ExternalId} đã có quyết định cuối",
            $"Quyết định: {decision}. Trạng thái: {report.Status}.",
            actorId,
            participantIds.Append(report.CreatedBy),
            new { report.ComplaintReportId, report.Status, Decision = decision },
            cancellationToken);

    private Task PublishAsync(
        ComplaintReport report,
        TopicNotifications topic,
        string title,
        string message,
        Guid actorId,
        IEnumerable<Guid?> recipients,
        object payload,
        CancellationToken cancellationToken)
        => _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = report.CompanyId,
            CreatedBy = actorId,
            Topic = topic,
            Title = title,
            Message = message,
            Link = $"/plm/complaint-reports/{report.ComplaintReportId}",
            AggregateId = report.ComplaintReportId,
            AggregateCode = report.ExternalId,
            PayloadJson = JsonSerializer.Serialize(payload),
            TargetUserIds = recipients
                .Where(x => x.HasValue && x.Value != Guid.Empty && x.Value != actorId)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray()
        }, cancellationToken);
}
