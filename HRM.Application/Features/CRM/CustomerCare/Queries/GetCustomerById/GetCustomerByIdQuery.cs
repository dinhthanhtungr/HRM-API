using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerById;

/// <summary>
/// Returns the editable customer profile when the current viewer can access the customer.
/// </summary>
public sealed record GetCustomerByIdQuery(Guid CustomerId)
    : IRequest<OperationResult<CustomerDetailDto>>;
