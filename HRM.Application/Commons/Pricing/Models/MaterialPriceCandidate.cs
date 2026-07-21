namespace HRM.Application.Commons.Pricing.Models
{
    public sealed class MaterialPriceCandidate
    {
        public Guid MaterialId { get; init; }
        public decimal CurrentPrice { get; init; }
        public DateTime? PriceDate { get; init; }
        public MaterialPriceSource PriceSource { get; init; } = MaterialPriceSource.Unknown;

        public bool HasValidPrice => CurrentPrice > 0;
    }
}
