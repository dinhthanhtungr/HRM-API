using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdDetails;

/// <summary>Báo cáo giao đủ đúng hạn theo dòng ĐHG, dựa trên phiếu giao không bị hủy.</summary>
public sealed class GetDeliveryOtdDetailsQuery : DeliveryOtdFilter, IRequest<OperationResult<DeliveryOtdDetailsDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public DeliveryOtdStatus? Status { get; init; }

}
