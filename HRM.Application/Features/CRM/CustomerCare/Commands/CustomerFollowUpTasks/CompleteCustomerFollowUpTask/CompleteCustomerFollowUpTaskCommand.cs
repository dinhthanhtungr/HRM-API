using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.CompleteCustomerFollowUpTask;

/// <summary>
/// Hoàn tất hoặc đóng follow-up task và lưu trạng thái kết thúc.
/// </summary>
public sealed class CompleteCustomerFollowUpTaskCommand : IRequest<OperationResult>
{
    public Guid TaskId { get; init; }
    public CompleteCustomerFollowUpTaskRequest Request { get; init; } = new();
}
