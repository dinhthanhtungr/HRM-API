using HRM.Application.Commons.Pricing.Models;

namespace HRM.Application.Commons.Pricing.Dtos
{
    public class LatestMaterialPriceDto
    {
        public Guid MaterialId { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTime? PriceDate { get; set; }
        public MaterialPriceSource PriceSource { get; set; } = MaterialPriceSource.Unknown;
    }
}
