using HRM.Application.Features.Groups.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.Groups.Commands.UpdateGroup;

public sealed class UpdateGroupCommand : IRequest<GroupCommandResult<GroupDto>>
{
    [JsonIgnore]
    public Guid GroupId { get; set; }

    public string? Name { get; init; }
    public string? GroupType { get; init; }
    public Guid PartId { get; init; }
}
