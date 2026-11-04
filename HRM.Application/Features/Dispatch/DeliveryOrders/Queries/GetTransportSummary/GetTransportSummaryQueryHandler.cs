using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetTransportSummary;

internal sealed class GetTransportSummaryQueryHandler(IDispatchReadDbContext db, ICurrentUser user)
    : IRequestHandler<GetTransportSummaryQuery, OperationResult<DeliveryTransportWorkbookData>>
{
    public async Task<OperationResult<DeliveryTransportWorkbookData>> Handle(GetTransportSummaryQuery request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanRead(user) || !DeliveryOrderAccessRules.CanManage(user) ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<DeliveryTransportWorkbookData>.Fail("Bạn không có quyền xem báo cáo vận chuyển.");

        if (request.From == default || request.To == default || request.From.Date > request.To.Date ||
            request.To.Date == DateTime.MaxValue.Date)
            return OperationResult<DeliveryTransportWorkbookData>.Fail("Khoảng ngày không hợp lệ.");

        return OperationResult<DeliveryTransportWorkbookData>.Ok(
            await DeliveryTransportReportQuery.BuildTransportWorkbookDataAsync(db, companyId, request.From, request.To, ct));
    }
}
