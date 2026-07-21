namespace HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas
{
    public sealed class FormulaId
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int ItemCount { get; set; }
        public DateTime? LastDateUse { get; set; }
    }
}
