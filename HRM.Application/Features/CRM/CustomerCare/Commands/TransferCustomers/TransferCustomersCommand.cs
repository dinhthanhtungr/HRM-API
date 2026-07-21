using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.TransferCustomers;

/// <summary>
/// Feature CRM CustomerCare - chuyển giao một lô hoặc toàn bộ khách hàng từ sale này sang sale khác
/// qua API POST `/api/v1/crm/customer-transfers`. Command ghi audit vào
/// `CustomerTransferLog`/`DetailCustomerTransfer` và cập nhật assignment hoặc claim.
/// </summary>
public sealed record TransferCustomersCommand(TransferCustomersRequest Request)
    : IRequest<OperationResult<TransferCustomersResultDto>>;
