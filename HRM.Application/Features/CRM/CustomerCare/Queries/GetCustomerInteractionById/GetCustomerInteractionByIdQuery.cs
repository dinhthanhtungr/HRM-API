using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerInteractionById;

/// <summary>
/// Lấy chi tiết một interaction khi người dùng hiện tại có quyền xem customer cha.
/// </summary>
public sealed record GetCustomerInteractionByIdQuery(Guid InteractionId)
    : IRequest<OperationResult<CustomerInteractionDto>>;
