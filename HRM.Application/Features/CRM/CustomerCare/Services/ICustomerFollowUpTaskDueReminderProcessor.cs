namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Quét và phát notification một lần cho các follow-up task CRM đã đến hạn.
/// </summary>
public interface ICustomerFollowUpTaskDueReminderProcessor
{
    Task<int> ProcessDueRemindersAsync(CancellationToken cancellationToken = default);
}
