using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class RawSummaryFormulaRow
    {
        public Guid FormulaId { get; set; }
        public string? ExternalId { get; set; }
        public string? Name { get; set; }
        public DateTime? FormulaSourceIdCreatedDate { get; set; }
        public List<RawSummaryFormulaMaterialRow> Materials { get; set; } = new();
        // thêm field để search
        public string? ColourCode { get; set; }
        public string? ColourName { get; set; }
        public string? CustomerName { get; set; }
        public string? SampleRequestExternalId { get; set; }
        public string? FormulaExternalId { get; set; }
        public string? MfgFormulaExternalId { get; set; }
    }
}
