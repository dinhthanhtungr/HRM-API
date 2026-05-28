using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema;

namespace HRM.Domain.Entities.SampleRequestSchema
{
    public class ColorChipRecordDevelopmentFormula
    {
        public Guid ColorChipRecordDevelopmentFormulaId { get; set; }

        public Guid ColorChipRecordId { get; set; }
        public Guid? DevelopmentFormulaId { get; set; }
        public bool IsActive { get; set; }
        public virtual Formula? DevelopmentFormula { get; set; }
        public virtual ColorChipRecord ColorChipRecord { get; set; } = default!;
    }
}
