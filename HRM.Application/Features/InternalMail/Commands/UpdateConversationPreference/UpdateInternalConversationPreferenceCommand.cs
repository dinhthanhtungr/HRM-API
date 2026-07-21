using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.UpdateConversationPreference;

public sealed class UpdateInternalConversationPreferenceCommand : IRequest<OperationResult>
{
    public Guid ConversationId { get; set; }
    public bool? IsArchived { get; set; }
    public bool? IsMuted { get; set; }
}
