using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.UpdateCustomerFollowUpTask;

/// <summary>
/// Cập nhật follow-up task CRM và đồng bộ ngày follow-up gần nhất của Customer.
/// </summary>
public sealed class UpdateCustomerFollowUpTaskCommand : IRequest<OperationResult>
{
    public Guid TaskId { get; init; }
    public UpdateCustomerFollowUpTaskRequest Request { get; init; } = new();
}
