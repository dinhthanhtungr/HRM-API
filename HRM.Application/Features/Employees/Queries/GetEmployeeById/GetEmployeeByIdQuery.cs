using HRM.Application.Features.Employees.Dtos;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Queries.GetEmployeeById
{
    public sealed class GetEmployeeByIdQuery : IRequest<EmployeeDetailDto?>
    {
        public GetEmployeeByIdQuery(Guid employeeId)
        {
            EmployeeId = employeeId;
        }

        public Guid EmployeeId { get; }
    }
}
