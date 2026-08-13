using HRM.Application.Features.Groups.Dtos;
using MediatR;

namespace HRM.Application.Features.Groups.Commands.SetGroupLeader;

public sealed record SetGroupLeaderCommand(Guid GroupId, Guid EmployeeId, bool IsLeader)
    : IRequest<GroupCommandResult<GroupMemberDto>>;
