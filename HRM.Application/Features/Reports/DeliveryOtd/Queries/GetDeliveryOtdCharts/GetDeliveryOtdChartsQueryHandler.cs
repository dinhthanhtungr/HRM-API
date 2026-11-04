using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdCharts;

internal sealed class GetDeliveryOtdChartsQueryHandler(IDispatchReadDbContext db, ICurrentUser user, IDateTimeProvider clock)
    : IRequestHandler<GetDeliveryOtdChartsQuery, OperationResult<DeliveryOtdChartsDto>>
{
    public async Task<OperationResult<DeliveryOtdChartsDto>> Handle(GetDeliveryOtdChartsQuery request, CancellationToken ct)
    {
        var today = clock.Now.Date;
        if (!DeliveryOtdReader.IsValid(request, user, today))
            return OperationResult<DeliveryOtdChartsDto>.Fail("Không có quyền hoặc bộ lọc OTD không hợp lệ.");
        var asOf = request.AsOf?.Date ?? today;
        var window = DeliveryOtdMonthlyTrend.Expand(request);
        if (window.To == DateTime.MaxValue.Date)
            return OperationResult<DeliveryOtdChartsDto>.Fail("Tháng đánh giá vượt phạm vi hỗ trợ.");
        var rows = await DeliveryOtdReader.ReadAsync(db, request, user.CompanyId!.Value, asOf, ct);
        var result = DeliveryOtdCalculator.Charts(rows, asOf);
        var trendRows = await DeliveryOtdReader.ReadAsync(db, window, user.CompanyId.Value, asOf, ct);
        var monthly = DeliveryOtdCalculator.Charts(trendRows, asOf).Monthly;
        var filled = DeliveryOtdMonthlyTrend.Fill(monthly, window, asOf);
        var rowsByMonth = trendRows.ToLookup(x => (x.DueDate ?? x.RequestedDate).ToString("yyyy-MM"));
        var poMonthly = new List<DeliveryOtdChartPoint>();
        foreach (var point in filled)
        {
            // Use all candidate rows, including missing promises, exactly as the header PO cohort does.
            var poSummary = await DeliveryOtdReader.ReadPoSummaryAsync(db, rowsByMonth[point.Key].ToList(),
                user.CompanyId.Value, asOf, ct);
            poMonthly.Add(point with { PoSummary = poSummary });
        }
        result = result with { Monthly = poMonthly };
        return OperationResult<DeliveryOtdChartsDto>.Ok(result);
    }
}
