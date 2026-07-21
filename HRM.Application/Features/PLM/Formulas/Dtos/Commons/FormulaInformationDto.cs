using HRM.Domain.Enums.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons
{
    public class FormulaInformationDto
    {
        public Guid FormulaId { get; set; }

        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = FormulaStatus.Draft.ToString();

        public Guid? CheckBy { get; set; }          // UNIQUEIDENTIFIER
        public string? CheckByName { get; set; }
        public DateTime? CheckDate { get; set; }       // DATETIME

        public Guid? SentBy { get; set; }          // UNIQUEIDENTIFIER
        public string? SentByName { get; set; }
        public DateTime? SentDate { get; set; }       // DATETIME

        public decimal? TotalPrice { get; set; }

        public DateTime? EffectiveDate { get; set; }
        public decimal? ProductionPrice { get; set; }
        public decimal? PresidentPrice { get; set; }
        public decimal? ProfitMarginPrice { get; set; }

        public bool IsSelect { get; set; }
        public bool IsActive { get; set; }

        public string? Note { get; set; }

        public DateTime? CreatedDate { get; set; }

        public IReadOnlyList<FormulaMaterialInformationDto> Materials { get; set; } 
            = new List<FormulaMaterialInformationDto>();
    }
}
