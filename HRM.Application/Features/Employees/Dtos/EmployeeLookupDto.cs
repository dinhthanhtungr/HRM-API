using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Dtos
{
    public sealed class EmployeeLookupDto
    {
        public Guid EmployeeId { get; init; }
        public string ExternalId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
    }
}
