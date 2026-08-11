using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDataChangeRequest;

/// <summary>
/// Sale đề xuất thay đổi các field kỹ thuật hoặc thông tin SampleRequest cần Lab xác nhận.
/// Command chỉ tạo action message trong thread; dữ liệu thật chỉ đổi sau khi người có quyền duyệt.
/// </summary>
public sealed class CreateSampleRequestDataChangeRequestCommand
    : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public Guid SampleRequestId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsUrgent { get; set; }
    public IReadOnlyList<Guid> RecipientEmployeeIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<SampleRequestProposedChangeRequestDto> Changes { get; set; }
        = Array.Empty<SampleRequestProposedChangeRequestDto>();
}
