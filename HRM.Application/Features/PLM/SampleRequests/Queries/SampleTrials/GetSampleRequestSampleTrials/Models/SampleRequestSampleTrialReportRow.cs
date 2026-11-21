using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;

internal sealed class SampleRequestSampleTrialReportRow
{
    public SampleRequest SampleRequest { get; set; } = null!;
    public SampleRequestSampleTrial? Trial { get; set; }
    public int? TrialCount { get; set; }
    public DateTime? LabFormulaConfirmedAt { get; set; }
    public Guid? ConfirmedFormulaId { get; set; }
    public bool HasPriceFeedback { get; set; }
    public bool HasAcceptedCustomerPrice { get; set; }
    public bool HasFinalizedChip { get; set; }
    public bool HasSalePriceWork { get; set; }
    public bool HasSaleFeedbackWork { get; set; }
    public Services.SampleWorkDecision? WorkDecision { get; set; }
    public bool NeedsSchedule { get; set; }
    public int WorkSortRank { get; set; }
    public bool WorkSortOverdue { get; set; }
    public DateTime? WorkSortDue { get; set; }
}
