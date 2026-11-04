using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Lab.Samples.Queries.GetLabSampleReport;

/// <summary>Báo cáo mẫu Lab theo ngày tạo yêu cầu, dùng audit mã màu và snapshot Lab approve.</summary>
internal sealed class GetLabSampleReportQueryHandler(IPLMReadDbContext db, ICustomerVisibilityService visibility, ICurrentUser user)
    : IRequestHandler<GetLabSampleReportQuery, OperationResult<LabSampleReportDto>>
{
    public async Task<OperationResult<LabSampleReportDto>> Handle(GetLabSampleReportQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is null || user.CompanyId == Guid.Empty
            || user.EmployeeId is null || user.EmployeeId == Guid.Empty
            || request.CompanyId.HasValue && request.CompanyId != user.CompanyId)
            return OperationResult<LabSampleReportDto>.Fail("Invalid current user/company scope.");
        var error = LabSampleReportRules.Validate(request);
        if (error != null) return OperationResult<LabSampleReportDto>.Fail(error);
        var trendStart = LabSampleReportPeriods.TrendStart(request.To);
        var cohortStart = request.From < trendStart ? request.From : trendStart;
        // Cùng query nguồn/scope và cutoff To; mỗi tập lọc riêng theo ngày tạo yêu cầu.
        var rows = await new LabSampleReportReader(db, visibility).QueryAsync(request, ct, cohortStart);
        var overviewStart = LabSampleReportPeriods.LocalMidnight(request.From);
        var overview = await rows.Where(x => x.CreatedAt >= overviewStart).GroupBy(x => 1)
            .Select(LabSampleAggregation.Projection).SingleOrDefaultAsync(ct);
        var trendFrom = LabSampleReportPeriods.LocalMidnight(trendStart);
        var monthly = await rows.Where(x => x.CreatedAt >= trendFrom).GroupBy(x => x.CreatedAt.Year * 100 + x.CreatedAt.Month)
            .Select(LabSampleAggregation.Projection).ToListAsync(ct);
        return OperationResult<LabSampleReportDto>.Ok(new LabSampleReportDto
        {
            OverviewPeriod = new(request.From, request.To), TrendPeriod = new(trendStart, request.To, 6),
            Overview = LabSampleAggregation.ToDto(overview), Months = LabSampleReportPeriods.BuildMonths(request.To, monthly)
        });
    }
}
