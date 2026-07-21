namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailFormulaReferencesDto
    {
        public Guid? SelectedDevelopmentFormulaId { get; set; }
        public string? SelectedDevelopmentFormulaExternalId { get; set; }
        public Guid? StandardManufacturingFormulaId { get; set; }
        public string? StandardManufacturingFormulaExternalId { get; set; }
        public IReadOnlyList<FormulaListDto> DevelopmentFormulas { get; set; } = [];
        public IReadOnlyList<SampleRequestDetailManufacturingFormulaDto> ManufacturingFormulas { get; set; } = [];
    }
}
