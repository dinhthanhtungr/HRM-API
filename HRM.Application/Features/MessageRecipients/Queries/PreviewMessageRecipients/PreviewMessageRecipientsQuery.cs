using HRM.Application.Commons.Models;
using HRM.Application.Features.MessageRecipients.Dtos;
using MediatR;

namespace HRM.Application.Features.MessageRecipients.Queries.PreviewMessageRecipients;

/// <summary>
/// Preview danh sách người nhận trước khi FE gửi một message/nghiệp vụ có thông báo.
/// Query không ghi dữ liệu; từng feature tự cài `IMessageRecipientResolver` theo `ContextType`.
/// </summary>
public sealed class PreviewMessageRecipientsQuery
    : IRequest<OperationResult<MessageRecipientPreviewDto>>
{
    public string ContextType { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public Guid? ContextId { get; set; }
    public Guid? DraftManagerBy { get; set; }
    public Guid? DraftCategoryId { get; set; }
    // null means first preview and lets the feature select optional defaults; [] means user removed them all.
    public IReadOnlyList<Guid>? SelectedRecipientEmployeeIds { get; set; }
    // Employees selected as thread watchers. They can read the thread but do not receive its notifications by default.
    public IReadOnlyList<Guid>? SelectedSilentWatcherEmployeeIds { get; set; }
}
