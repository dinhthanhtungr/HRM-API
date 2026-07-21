using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.CreateConversation;

/// <summary>
/// Tao mail noi bo tu do. Conversation bam vao nghiep vu cu the phai duoc tao qua feature tuong ung.
/// </summary>
public sealed class CreateInternalConversationCommand : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<Guid> RecipientEmployeeIds { get; set; } = Array.Empty<Guid>();
    public bool IsUrgent { get; set; }
}
