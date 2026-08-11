using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.DeactivateCustomer;

/// <summary>
/// Ngừng hoạt động một khách hàng mà không cho FE patch trực tiếp IsActive.
/// </summary>
public sealed record DeactivateCustomerCommand(Guid CustomerId)
    : IRequest<OperationResult<CustomerActivationResultDto>>;
