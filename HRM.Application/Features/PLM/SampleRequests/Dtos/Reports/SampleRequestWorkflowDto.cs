using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed record SampleRequestPriceReferenceDto(Guid QuotationLineId, string Label);
public sealed record SampleRequestWorkflowDto(SampleRequestSampleTrialReportDto Item,
    IReadOnlyList<SampleRequestPriceReferenceDto> PriceReferences);
