using HRM.Application.Features.PLM.SampleRequests.Dtos.Detail;
using HRM.Domain.Enums.Merchadises;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;

public sealed class GetSampleRequestDetailQuery : IRequest<SampleRequestDetailDto?>
{
    public Guid SampleRequestId { get; set; }
    /// <summary>
    /// Chỉ bật khi FE đang đọc item vừa chọn trong dropdown lập Sale Order.
    /// Backend vẫn tự kiểm tra customer, company, status và quan hệ Sample Request.
    /// </summary>
    public bool ForSaleOrder { get; set; }
    public Guid? CustomerId { get; set; }
    public OrderType? OrderType { get; set; }
}
