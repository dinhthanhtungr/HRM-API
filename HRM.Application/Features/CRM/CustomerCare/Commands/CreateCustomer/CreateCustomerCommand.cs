using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomer;

/// <summary>
/// Tạo hồ sơ khách hàng tiềm năng trong công ty hiện tại và tạo claim Work cho sale đang thao tác.
/// Endpoint tạo mới không cho FE tự chuyển thẳng thành khách đã sale; việc convert phải đi qua API lead convert.
/// </summary>
public sealed record CreateCustomerCommand(CreateCustomerRequest Request)
    : IRequest<OperationResult<CustomerCreateResultDto>>;
