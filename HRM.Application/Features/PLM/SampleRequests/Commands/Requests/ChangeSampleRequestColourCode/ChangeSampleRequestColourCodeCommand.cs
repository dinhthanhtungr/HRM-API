using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ChangeSampleRequestColourCode;

/// <summary>
/// Sinh lại mã màu theo quy tắc tạo Product lần đầu khi sản phẩm chưa từng gửi mẫu,
/// đồng bộ dữ liệu nháp/lịch sử liên quan và tạo message thông báo thay đổi.
/// </summary>
public sealed class ChangeSampleRequestColourCodeCommand
    : IRequest<OperationResult<ChangeSampleRequestColourCodeResultDto>>
{
    public Guid SampleRequestId { get; set; }
    public string ColourCode { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public DateTime? ExpectedUpdatedDate { get; set; }
    public bool IsUrgent { get; set; }
    public IReadOnlyList<Guid> RecipientEmployeeIds { get; set; } = Array.Empty<Guid>();
}
