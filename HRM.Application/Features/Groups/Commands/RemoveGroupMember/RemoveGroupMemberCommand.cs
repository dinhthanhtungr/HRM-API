using HRM.Application.Features.Groups.Dtos;
using MediatR;

namespace HRM.Application.Features.Groups.Commands.RemoveGroupMember;

public sealed record RemoveGroupMemberCommand(Guid GroupId, Guid EmployeeId)
    : IRequest<GroupCommandResult<GroupMemberDto>>;
