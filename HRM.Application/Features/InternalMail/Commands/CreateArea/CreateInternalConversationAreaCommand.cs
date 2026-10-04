using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.CreateArea;

/// <summary>Create a private child thread; only selected employees join, with no role defaults.</summary>
public sealed class CreateInternalConversationAreaCommand : IRequest<OperationResult<InternalMailAreaDto>>
{
    [JsonIgnore] public Guid ConversationId { get; set; }
    public string AreaCode { get; set; } = string.Empty;
    public IReadOnlyList<Guid> EmployeeIds { get; set; } = Array.Empty<Guid>();
}
