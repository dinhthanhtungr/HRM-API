using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.RepairCustomerAssignmentGroups;

/// <summary>
/// Đồng bộ GroupId của các CustomerAssignment active theo group active của Sale trong công ty hiện tại.
/// Mặc định chỉ preview; chỉ ghi dữ liệu khi DryRun được truyền rõ là false.
/// </summary>
public sealed record RepairCustomerAssignmentGroupsCommand(
    Guid EmployeeId,
    Guid? TargetGroupId = null,
    bool DryRun = true)
    : IRequest<OperationResult<RepairCustomerAssignmentGroupsResult>>;

public sealed record RepairCustomerAssignmentGroupsResult(
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeName,
    Guid TargetGroupId,
    string? TargetGroupName,
    bool DryRun,
    int ActiveAssignmentCount,
    int AlreadyCorrectCount,
    int CandidateCount,
    int ConflictCount,
    int UpdatedCount,
    IReadOnlyList<CustomerAssignmentGroupRepairPreviewItem> PreviewItems,
    bool HasMorePreviewItems);

public sealed record CustomerAssignmentGroupRepairPreviewItem(
    Guid AssignmentId,
    Guid CustomerId,
    string CustomerExternalId,
    string CustomerName,
    Guid CurrentGroupId,
    string? CurrentGroupName,
    bool HasTargetGroupConflict);
