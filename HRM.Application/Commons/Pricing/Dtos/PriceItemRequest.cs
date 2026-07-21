using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Dtos
{
    public class PriceItemRequest
    {
        public ItemType ItemType { get; set; }
        public Guid? MaterialId { get; set; }
        public Guid? ProductId { get; set; }
    }
}
