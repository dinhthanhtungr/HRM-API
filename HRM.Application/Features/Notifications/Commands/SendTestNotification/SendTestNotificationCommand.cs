using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Notifications.Commands.SendTestNotification;

/// <summary>Tạo thông báo kiểm thử cố định cho Developer active trong công ty hiện tại.</summary>
public sealed record SendTestNotificationCommand : IRequest<OperationResult<Guid>>;
