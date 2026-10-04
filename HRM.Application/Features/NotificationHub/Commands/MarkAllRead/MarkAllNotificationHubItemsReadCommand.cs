using HRM.Application.Features.NotificationHub.Dtos;
using MediatR;

namespace HRM.Application.Features.NotificationHub.Commands.MarkAllRead;

/// <summary>
/// Đánh dấu đồng thời notification và tin nhắn trong các conversation current employee được phép đọc.
/// </summary>
public sealed record MarkAllNotificationHubItemsReadCommand
    : IRequest<NotificationHubMarkAllReadResultDto>;
