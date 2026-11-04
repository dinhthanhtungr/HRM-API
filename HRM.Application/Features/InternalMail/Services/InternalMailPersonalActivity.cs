using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.InternalMail.Services;

internal static class InternalMailPersonalActivity
{
    internal static readonly TopicNotifications[] ReactionTopics = NotificationTopicCatalog.GetTopics(null, "reaction").ToArray();
}
