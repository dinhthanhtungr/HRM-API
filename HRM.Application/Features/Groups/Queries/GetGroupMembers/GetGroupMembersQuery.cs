using HRM.Application.Features.Groups.Dtos;
using MediatR;

namespace HRM.Application.Features.Groups.Queries.GetGroupMembers;

/// <summary>
/// Trả các thành viên active của một nhóm thuộc công ty hiện tại.
/// </summary>
public sealed record GetGroupMembersQuery(Guid GroupId) : IRequest<GroupMembersDto?>;
