using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

public enum SampleRequestAdminMetric { Received, Completed, Open, Cancelled, InProgress, Pending, PriceAccepted, ChipAccepted, SampleAccepted,
    CompletedQuotation, CompletedChip, CompletedSample, CompletedOther, LabDeclined }

public sealed class GetSampleRequestAdminSummaryQuery : IRequest<SampleRequestAdminSummaryDto>
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
}
