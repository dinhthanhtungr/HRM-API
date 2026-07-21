using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public class FormulaListDto
    {
        public Guid FormulaId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
        public decimal? ProductionPrice { get; set; }
        public decimal? PresidentPrice { get; set; }
        public decimal? ProfitMarginPrice { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public bool IsSelect { get; set; }
        public int MaterialCount { get; set; }
        public string MaterialsUrl { get; set; } = string.Empty;
    }
}
