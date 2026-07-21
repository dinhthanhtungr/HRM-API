namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailFormulaLookupDto
    {
        public Guid FormulaId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
    }
}
