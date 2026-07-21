using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.Customers.Dtos.GetCustomerLookup;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.CRM.Customers.Queries.GetCustomerLookup;

public sealed class GetCustomerLookupQuery 
    : PaginationQuery, IRequest<PagedResult<CustomerLookupDto>>
{
    public bool? IsLead { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    public Guid? GroupId { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? CustomerId { get; init; }
    public LeadStatus? LeadStatus { get; init; }
    public string? CurrentCrmStatus { get; init; }
    public bool? IsActive { get; init; } = true;
}
