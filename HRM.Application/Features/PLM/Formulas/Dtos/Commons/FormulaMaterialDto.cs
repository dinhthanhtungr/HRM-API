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

        public string? MaterialNameSnapshot { get; set; }
        public string? MaterialExternalIdSnapshot { get; set; }
    }
}
