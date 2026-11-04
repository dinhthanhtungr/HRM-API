using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetTransportSummary;

/// <summary>Đọc bảng tổng hợp và chi tiết vận chuyển theo người giao, cùng dữ liệu với Excel.</summary>
public sealed record GetTransportSummaryQuery(DateTime From, DateTime To)
    : IRequest<OperationResult<DeliveryTransportWorkbookData>>;
