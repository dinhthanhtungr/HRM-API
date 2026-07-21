using HRM.Domain.Enums.Formulas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Pricing.Dtos
{
    public class LatestItemPriceDto
    {
        public ItemType ItemType { get; set; }
        public Guid ItemId { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTime? PriceDate { get; set; }
        public LatestPriceSourceType PriceSource { get; set; } = LatestPriceSourceType.Unknown;
    }
}
