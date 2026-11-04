using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SetLegacyWorkTracking;

/// <summary>Admin chọn hoặc bỏ hồ sơ trước mốc vận hành khỏi hàng đợi công việc Lab/Sale.</summary>
public sealed class SetLegacyWorkTrackingCommand : IRequest<OperationResult<Guid>>
{
    public Guid SampleRequestId { get; set; }
    public bool Included { get; set; }
    public DateTime? ExpectedUpdatedDate { get; set; }
}
