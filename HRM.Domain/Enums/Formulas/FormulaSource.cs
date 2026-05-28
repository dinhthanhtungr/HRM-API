using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Formulas
{
    public enum FormulaSource
    {
        FromVA = 0,   // ManufacturingFormulaRepository
        FromVU = 1,     // FormulaRepository
        Both = 2
    }
}
