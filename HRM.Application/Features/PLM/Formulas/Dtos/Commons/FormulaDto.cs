using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons
{
    public class FormulaDto
    {
        public Guid FormulaId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;

        public IReadOnlyList<FormulaMaterialDto> Items { get; set; }
            = new List<FormulaMaterialDto>();
    }
}
