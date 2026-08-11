using HRM.Application.Features.Groups.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.Groups.Commands.AddGroupMember;

/// <summary>
/// Thêm thành viên active vào nhóm hoặc cập nhật quyền leader của thành viên đang active.
/// </summary>
public sealed class AddGroupMemberCommand : IRequest<GroupCommandResult<GroupMemberDto>>
{
    [JsonIgnore]
    public Guid GroupId { get; set; }
    public Guid EmployeeId { get; init; }
    public bool IsLeader { get; init; }
}
