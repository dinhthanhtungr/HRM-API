using HRM.Application.Features.Groups.Dtos;
using MediatR;

namespace HRM.Application.Features.Groups.Queries.GetGroups;

/// <summary>
/// Trả toàn bộ nhóm thuộc công ty hiện tại cùng số thành viên và leader active.
/// </summary>
public sealed record GetGroupsQuery : IRequest<IReadOnlyList<GroupDto>>;
