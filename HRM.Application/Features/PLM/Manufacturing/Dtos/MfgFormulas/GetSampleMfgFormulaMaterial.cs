using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class GetSampleMfgFormulaMaterial
    {
        public Guid ItemId { get; set; }
        public ItemType itemType { get; set; }
        public Guid CategoryId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? MaterialNameSnapshot { get; set; } // NVARCHAR
        public string? MaterialExternalIdSnapshot { get; set; } // VARCHAR
        public string? Unit { get; set; } // VARCHAR
    }
}
