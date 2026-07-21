using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.ArchiveCustomerFollowUpTask;

/// <summary>
/// Archive follow-up task bằng soft delete để giữ lịch sử chăm sóc khách hàng.
/// </summary>
public sealed record ArchiveCustomerFollowUpTaskCommand(Guid TaskId) : IRequest<OperationResult>;
