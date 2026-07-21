using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailDto
    {
        public SampleRequestDetailHeroDto Hero { get; set; } = new();
        public SampleRequestDetailQuickSummaryDto QuickSummary { get; set; } = new();
        public SampleRequestDetailRequirementCardDto SampleRequestRequirement { get; set; } = new();
        public SampleRequestDetailTechnicalRequirementCardDto TechnicalRequirement { get; set; } = new();
        public IReadOnlyList<SampleRequestDetailFormulaLookupDto> FormulaLookups { get; set; } = [];
        public IReadOnlyList<SampleRequestAttachmentDto> Attachments { get; set; } = [];
    }
}
