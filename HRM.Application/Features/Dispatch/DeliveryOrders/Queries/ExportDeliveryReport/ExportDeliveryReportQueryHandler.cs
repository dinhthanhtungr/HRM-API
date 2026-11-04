using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;

internal sealed class ExportDeliveryReportQueryHandler(
    IDispatchReadDbContext db, ICurrentUser user, IDeliveryReportExcelRenderer renderer)
    : IRequestHandler<ExportDeliveryReportQuery, OperationResult<DeliveryOrderFileDto>>
{
    public async Task<OperationResult<DeliveryOrderFileDto>> Handle(
        ExportDeliveryReportQuery request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanRead(user) ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            (request.Kind == DeliveryReportKind.Transport && !DeliveryOrderAccessRules.CanManage(user)))
            return OperationResult<DeliveryOrderFileDto>.Fail("Bạn không có quyền xuất báo cáo giao hàng.");

        if (!Enum.IsDefined(request.Kind) || request.From == default || request.To == default ||
            request.From.Date > request.To.Date || request.To.Date == DateTime.MaxValue.Date)
            return OperationResult<DeliveryOrderFileDto>.Fail("Khoảng ngày hoặc loại báo cáo không hợp lệ.");

        var bytes = request.Kind switch
        {
            DeliveryReportKind.Plan => renderer.ExportDeliveryPlanExcel(
                await DeliveryPlanReportQuery.BuildRowsAsync(db, companyId, request.From, request.To, ct)),
            DeliveryReportKind.Finish => renderer.ExportDeliveryFinish(
                await DeliveryFinishReportQuery.BuildDeliveryFinishRowsAsync(db, companyId, request.From, request.To, ct), request.From, request.To),
            _ => renderer.ExportTransportWorkbook(
                await DeliveryTransportReportQuery.BuildTransportWorkbookDataAsync(db, companyId, request.From, request.To, ct), request.From, request.To)
        };
        return OperationResult<DeliveryOrderFileDto>.Ok(new DeliveryOrderFileDto(
            request.Kind == DeliveryReportKind.Transport
                ? $"TongHopVanChuyen_{request.From:yyyyMMdd}_{request.To:yyyyMMdd}.xlsx"
                : $"delivery-{request.Kind.ToString().ToLowerInvariant()}-{request.From:yyyyMMdd}-{request.To:yyyyMMdd}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes));
    }
}
