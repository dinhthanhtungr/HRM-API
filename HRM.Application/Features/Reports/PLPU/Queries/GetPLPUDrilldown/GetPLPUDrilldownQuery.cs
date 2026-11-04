using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUDrilldown;

/// <summary>Danh sách PO/dòng đóng góp vào KPI, cùng scope và cohort ngày tạo PO của header.</summary>
public sealed class GetPLPUDrilldownQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUDrilldownDto>>
{
    public string? Metric { get; set; }
    /// <summary>Computed report status code, supported only with Metric=totalPo; distinct from persisted Status.</summary>
    public string? ReportStatus { get; set; }
}
