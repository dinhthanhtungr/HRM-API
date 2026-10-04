using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestPeriodReport;

/// <summary>Đếm kết quả từng Trial theo ngày phản hồi; delay là cờ hiện tại, không phải lịch sử.</summary>
public sealed class GetSampleRequestPeriodReportQuery : IRequest<SampleRequestPeriodReportDto>
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
}
