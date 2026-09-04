using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.DirectPatchNotifications;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDirectPatchNotification;

/// <summary>
/// Gửi thông báo sau khi FE đã PATCH trực tiếp các field `sample_request.*` hoặc `product.*`.
/// Command không cập nhật dữ liệu nghiệp vụ; dữ liệu thật phải được lưu thành công bằng PATCH trước đó.
/// </summary>
public sealed class CreateSampleRequestDirectPatchNotificationCommand
    : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public Guid SampleRequestId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsUrgent { get; set; }
    public IReadOnlyList<Guid> RecipientEmployeeIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<SampleRequestDirectPatchChangeDto> Changes { get; set; }
        = Array.Empty<SampleRequestDirectPatchChangeDto>();
}
