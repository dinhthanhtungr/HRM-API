using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class FmItemRow
    {
        public Guid ItemId { get; set; }
        public ItemType itemType { get; set; }
        public Guid? CategoryId { get; init; }
        public decimal Quantity { get; init; }
        public string Unit { get; init; } = "";
        public decimal? UnitPrice { get; init; }
        public string? MaterialNameSnapshot { get; init; }
        public string? MaterialExternalIdSnapshot { get; init; }
    }
}
