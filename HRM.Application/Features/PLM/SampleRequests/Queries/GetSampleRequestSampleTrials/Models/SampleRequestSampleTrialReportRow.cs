using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;

internal sealed class SampleRequestSampleTrialReportRow
{
    public SampleRequest SampleRequest { get; set; } = null!;
    public SampleRequestSampleTrial? Trial { get; set; }
    public int? TrialCount { get; set; }
}
