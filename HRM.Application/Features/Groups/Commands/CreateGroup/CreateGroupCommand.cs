using HRM.Application.Features.Groups.Dtos;
using MediatR;

namespace HRM.Application.Features.Groups.Commands.CreateGroup;

/// <summary>
/// Tạo nhóm mới trong bộ phận bắt buộc; company, người tạo và mã GRP theo tháng do backend xác định.
/// </summary>
public sealed class CreateGroupCommand : IRequest<GroupCommandResult<GroupDto>>
{
    public string? Name { get; init; }
    public string? GroupType { get; init; }
    public Guid PartId { get; init; }
}
