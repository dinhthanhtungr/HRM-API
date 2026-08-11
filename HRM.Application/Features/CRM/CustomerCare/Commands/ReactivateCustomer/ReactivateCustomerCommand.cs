using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ReactivateCustomer;

/// <summary>
/// Khôi phục một khách hàng đã ngừng hoạt động mà không cho FE patch trực tiếp IsActive.
/// </summary>
public sealed record ReactivateCustomerCommand(Guid CustomerId)
    : IRequest<OperationResult<CustomerActivationResultDto>>;
