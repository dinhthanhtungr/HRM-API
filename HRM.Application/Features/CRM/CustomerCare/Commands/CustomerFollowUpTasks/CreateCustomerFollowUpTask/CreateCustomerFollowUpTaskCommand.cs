using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.CreateCustomerFollowUpTask;

/// <summary>
/// Tạo follow-up task CRM và liên kết task với Customer/Interaction bằng WorkTaskReference.
/// </summary>
public sealed class CreateCustomerFollowUpTaskCommand : IRequest<OperationResult<Guid>>
{
    public CreateCustomerFollowUpTaskRequest Request { get; init; } = new();
}
