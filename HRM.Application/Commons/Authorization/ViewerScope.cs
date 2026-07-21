using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Authorization
{
    public sealed record ViewerScope(
        Guid CompanyId,
        Guid EmployeeId,
        bool HasFullCustomerView,
        IReadOnlySet<Guid> LeaderGroupIds,
        IReadOnlySet<Guid> EmployeeIdsInScope,
        DateTime Now);
}
