using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.ForwardToGroup;

public sealed class ForwardToInternalGroupCommand : IRequest<OperationResult<InternalMailAreaDto>>
{
    [JsonIgnore] public Guid ConversationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<Guid> EmployeeIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> MessageIds { get; set; } = Array.Empty<Guid>();
}
