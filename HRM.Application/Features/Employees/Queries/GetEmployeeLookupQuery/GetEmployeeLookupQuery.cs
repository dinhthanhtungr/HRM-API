using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Queries.GetEmployeeDropdown
{
    public sealed class GetEmployeeLookupQuery 
        : PaginationQuery, IRequest<PagedResult<EmployeeLookupDto>>
    {
        public string? Search { get; init; }
        public Guid? PartId { get; init; }
        public Guid? GroupId { get; init; }
    }
}
