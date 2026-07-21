using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Models;

internal sealed class SampleRequestSummaryProjection
{
    public Guid? ProductId { get; set; }
    public Guid AttachmentCollectionId { get; set; }
    public SampleRequestSummaryDto Summary { get; set; } = new();
}

internal sealed class ProductionOrderProjection
{
    public Guid ProductId { get; set; }
    public Guid MfgProductionOrderId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string FormulaExternalId { get; set; } = string.Empty;
}

internal sealed class SelectedManufacturingFormulaProjection
{
    public Guid MfgProductionOrderId { get; set; }
    public SampleRequestSelectedManufacturingFormulaDto Formula { get; set; } = new();
}
