using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Models;

internal sealed class SampleRequestSummaryProjection
{
    public Guid CompanyId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid AttachmentCollectionId { get; set; }
    public SampleRequestSummaryDto Summary { get; set; } = new();
}
