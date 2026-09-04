using HRM.Domain.Enums.Formulas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons
{
    public class FormulaMaterialDto
    {
        public Guid? FormulaMaterialId { get; set; }
        public int LineNo { get; set; }

        public Guid ItemId { get; set; }
        public ItemType ItemType { get; set; }

        public Guid? CategoryId { get; set; }
        public decimal Quantity { get; set; }

        public LatestPriceSource Price { get; set; } = new();
        public decimal PriceTotal { get; set; }
        public bool HasLatestPrice { get; set; }
        public decimal? LatestUnitPrice { get; set; }
        public decimal? LatestTotalPrice { get; set; }
        public DateTime? LatestPriceDate { get; set; }
        public LatestPriceSourceType LatestPriceSource { get; set; } = LatestPriceSourceType.Unknown;

        public string? MaterialNameSnapshot { get; set; }
        public string? MaterialExternalIdSnapshot { get; set; }
    }
}
