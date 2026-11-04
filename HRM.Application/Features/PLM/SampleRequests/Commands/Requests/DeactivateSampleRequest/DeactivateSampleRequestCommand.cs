using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DeactivateSampleRequest;

/// <summary>Sale vô hiệu hóa yêu cầu do mình phụ trách, chưa gửi mẫu và chưa hoàn thành.</summary>
public sealed class DeactivateSampleRequestCommand : IRequest<OperationResult<Guid>>
{
    public Guid SampleRequestId { get; set; }
}
