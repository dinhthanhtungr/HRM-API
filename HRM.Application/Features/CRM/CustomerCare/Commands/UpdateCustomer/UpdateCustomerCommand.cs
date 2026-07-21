using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomer;

/// <summary>
/// Feature CRM CustomerCare - cập nhật hồ sơ khách hàng qua API PATCH
/// `/api/v1/crm/customers/{customerId}`. Request chỉ patch các field được gửi,
/// bao gồm thông tin hồ sơ, địa chỉ, liên hệ và ghi chú của employee hiện tại.
/// </summary>
public sealed record UpdateCustomerCommand(Guid CustomerId, UpdateCustomerRequest Request)
    : IRequest<OperationResult>;
