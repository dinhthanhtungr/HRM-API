using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class SilentWatcherReconciliationRules
{
    internal static string Status(bool eligible, bool notificationRecipient, InternalConversationParticipant? current)
        => current is not null ? !current.IsActive ? "Removed" :
            current.Role == InternalConversationParticipantRole.Watcher && current.IsMuted ? "ExistingWatcher" : "ExistingMember"
            : !eligible ? "Ineligible" : notificationRecipient ? "NotificationRecipient" : "Missing";
}
