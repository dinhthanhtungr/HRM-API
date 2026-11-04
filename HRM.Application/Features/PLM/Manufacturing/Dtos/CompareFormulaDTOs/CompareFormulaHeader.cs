using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.CompareFormulaDTOs
{
    public sealed class CompareFormulaHeader
    {
        public FormulaSource Source { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid Id { get; set; }
        public string? ExternalId { get; set; }
        public string? ColourCode { get; set; }
    }
}
