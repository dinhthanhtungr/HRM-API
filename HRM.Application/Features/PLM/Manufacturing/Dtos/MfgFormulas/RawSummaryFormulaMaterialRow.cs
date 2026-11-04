using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class RawSummaryFormulaMaterialRow
    {
        public ItemType itemType { get; set; }
        public Guid ItemId { get; set; }
        public Guid? MaterialId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid CategoryId { get; set; }
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
        public string? MaterialNameSnapshot { get; set; }
        public string? MaterialExternalIdSnapshot { get; set; }
        public decimal? FallbackUnitPrice { get; set; }
    }
}
