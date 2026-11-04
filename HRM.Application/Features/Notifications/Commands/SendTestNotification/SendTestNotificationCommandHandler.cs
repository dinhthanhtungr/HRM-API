using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.Notifications;
using MediatR;

namespace HRM.Application.Features.Notifications.Commands.SendTestNotification;

internal sealed class SendTestNotificationCommandHandler(
    ICurrentUser user,
    INotificationService notifications)
    : IRequestHandler<SendTestNotificationCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(SendTestNotificationCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !user.IsInRole(ApplicationRoles.Developer))
            return OperationResult<Guid>.Fail("Chỉ Developer được gửi thông báo kiểm thử.");

        if (user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<Guid>.Fail("Tài khoản phải được liên kết với công ty và nhân viên.");

        // Không thêm user/role/team khác: publish tự resolve Developer active cùng company.
        var id = await notifications.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = companyId,
            CreatedBy = employeeId,
            Topic = TopicNotifications.DeveloperNotificationTest,
            Title = "Kiểm thử thông báo Developer",
            Message = "Thông báo kiểm thử inbox và kết nối realtime, chỉ gửi đến Developer trong công ty."
        }, ct);

        return OperationResult<Guid>.Ok(id, "Đã tạo thông báo kiểm thử cho Developer.");
    }
}
