using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomers
{
    public sealed class GetCustomersQuery
        : PaginationQuery, IRequest<PagedResult<CustomerListItemDto>>
    {
        public Guid? CompanyId { get; init; }
        public Guid? SaleEmployeeId { get; init; }
        public Guid? GroupId { get; init; }

        public bool? IsLead { get; init; }
        public LeadStatus? LeadStatus { get; init; }

        public string? CurrentCrmStatus { get; init; }
        public bool? IsActive { get; init; }
        public bool IncludeInactive { get; init; }

        public DateTime? NextFollowUpFrom { get; init; }
        public DateTime? NextFollowUpTo { get; init; }
    }
}
