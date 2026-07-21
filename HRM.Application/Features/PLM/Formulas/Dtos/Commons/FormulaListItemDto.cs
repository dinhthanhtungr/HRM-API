using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons
{
    public class FormulaListItemDto
    {
        public Guid FormulaId { get; set; }
        public string? ExternalId { get; set; }
        public string? Name { get; set; }

        public DateTime? CreatedDate { get; set; }
        public bool IsSelect { get; set; }
        public string? Note { get; set; }

        public decimal? TotalPrice { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public bool IsStandard { get; set; }
        public bool IsCurrentStandard { get; set; }
        public Guid? ProductStandardFormulaId { get; set; }
        public Guid? MfgProductionOrderId { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public List<FormulaMaterialDto> Materials { get; set; } = new();
    }
}
