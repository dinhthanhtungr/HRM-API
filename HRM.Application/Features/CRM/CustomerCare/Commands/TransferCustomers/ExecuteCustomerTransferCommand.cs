using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.TransferCustomers;

/// <summary>
/// Chuyển giao khách hàng theo flow mới cho FE: BE tự phân loại lead/khách đã sale và ghi log phù hợp.
/// </summary>
public sealed record ExecuteCustomerTransferCommand(ExecuteCustomerTransferRequest Request)
    : IRequest<OperationResult<ExecuteCustomerTransferResultDto>>;
