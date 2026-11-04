using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;

/// <summary>
/// Phân loại lý do không thể tải detail Sample Request mà không trả dữ liệu
/// của record không thuộc company hiện tại.
/// </summary>
public sealed class GetSampleRequestDetailAccessQuery
    : IRequest<SampleRequestDetailAccessStatus>
{
    public Guid SampleRequestId { get; init; }
}

public enum SampleRequestDetailAccessStatus
{
    NotFound = 0,
    Forbidden = 1,
    InvalidRelationship = 2
}
