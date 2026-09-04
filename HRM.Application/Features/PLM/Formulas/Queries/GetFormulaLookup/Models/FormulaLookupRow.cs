using HRM.Domain.Enums.Formulas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaLookup.Models
{
    internal sealed class FormulaLookupRow
    {
        public Guid FormulaId { get; init; }
        public string ColorCode { get; set; } = string.Empty;
        public FormulaSource SourceType { get; init; }
        public string ExternalId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? Note { get; init; }
        public int MaterialCount { get; init; }
        public DateTime? CreatedDate { get; init; }
    }
}
