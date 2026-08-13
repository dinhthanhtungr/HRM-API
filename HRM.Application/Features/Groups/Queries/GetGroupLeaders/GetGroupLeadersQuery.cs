using HRM.Application.Features.Groups.Dtos;
using MediatR;

namespace HRM.Application.Features.Groups.Queries.GetGroupLeaders;

public sealed record GetGroupLeadersQuery(Guid GroupId) : IRequest<GroupMembersDto?>;
