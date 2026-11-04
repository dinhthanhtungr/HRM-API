using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdHeader;

/// <summary>Báo cáo giao đủ đúng hạn theo dòng ĐHG, dựa trên phiếu giao không bị hủy.</summary>
public sealed class GetDeliveryOtdHeaderQuery : DeliveryOtdFilter, IRequest<OperationResult<DeliveryOtdHeaderDto>>
{
}
