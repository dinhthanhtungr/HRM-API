using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Manufacturings
{
    public enum ManufacturingProductOrder
    {
        Unknown,
        New,
        FormulaRequested,
        FormulaSuccess,
        Scheduling,
        Scheduled,

        Waiting,
        Unassign,
        Getback,


        QCinprogress,
        QCPassed,
        QCFail,

        BTPNew,
        Weighting,
        Weighted,
        Mixing,
        Mixed,

        Started,
        Canceled,
        Running,
        Finished,

        Change,
        Unassignfrommd,
        Reported,
        Done,

        Stocked
    }

}
