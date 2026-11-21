using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Services;

internal static class PersonalReminderAudience
{
    public static bool IsOwnerOnly(PublishNotificationRequest request, Guid createdBy)
    {
        if (request.PersonalReminderOwnerId is not { } owner) return false;
        if (owner == Guid.Empty || owner != createdBy || request.Topic != TopicNotifications.WorkTaskDue ||
            request.ConversationId.HasValue || request.TargetUserIds?.Count != 1 ||
            !request.TargetUserIds.Contains(owner) || request.TargetRoles?.Count > 0 ||
            request.TargetTeamIds?.Count > 0 || request.SilentUserIds?.Count > 0)
            throw new InvalidOperationException("Personal reminders require a single owner recipient.");
        return true;
    }
}
