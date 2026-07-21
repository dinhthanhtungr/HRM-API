using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Commands.RegisterWebPushSubscription;

internal sealed class RegisterWebPushSubscriptionCommandHandler
    : IRequestHandler<RegisterWebPushSubscriptionCommand, OperationResult<WebPushSubscriptionDto>>
{
    private readonly INotificationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterWebPushSubscriptionCommandHandler(
        INotificationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<WebPushSubscriptionDto>> Handle(
        RegisterWebPushSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        var endpoint = request.Endpoint?.Trim();
        var p256dh = request.Keys?.P256dh?.Trim();
        var auth = request.Keys?.Auth?.Trim();

        if (!WebPushSubscriptionRules.IsValidEndpoint(endpoint))
        {
            return OperationResult<WebPushSubscriptionDto>.Fail("A valid HTTPS push endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(p256dh) || p256dh.Length > WebPushSubscriptionRules.MaxP256dhLength ||
            string.IsNullOrWhiteSpace(auth) || auth.Length > WebPushSubscriptionRules.MaxAuthLength)
        {
            return OperationResult<WebPushSubscriptionDto>.Fail("Push subscription keys are invalid.");
        }

        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (!companyId.HasValue || companyId.Value == Guid.Empty ||
            !employeeId.HasValue || employeeId.Value == Guid.Empty)
        {
            return OperationResult<WebPushSubscriptionDto>.Fail("Current employee or company is invalid.");
        }

        var employeeExists = await _dbContext.Employees
            .AsNoTracking()
            .AnyAsync(x =>
                x.EmployeeId == employeeId.Value &&
                x.CompanyId == companyId.Value &&
                x.IsActive,
                cancellationToken);
        if (!employeeExists)
        {
            return OperationResult<WebPushSubscriptionDto>.Fail("Current employee is not active in this company.");
        }

        var subscription = await _dbContext.WebPushSubscriptions
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint, cancellationToken);

        var now = _dateTimeProvider.Now;
        if (subscription is null)
        {
            subscription = new WebPushSubscription
            {
                WebPushSubscriptionId = Guid.CreateVersion7(),
                CreatedAt = now,
                Endpoint = endpoint!
            };
            await _dbContext.WebPushSubscriptions.AddAsync(subscription, cancellationToken);
        }

        // Cung browser dang nhap tai khoan moi se chuyen subscription sang current employee,
        // tranh tiep tuc gui du lieu cho tai khoan da logout tren thiet bi dung chung.
        subscription.CompanyId = companyId.Value;
        subscription.EmployeeId = employeeId.Value;
        subscription.P256dh = p256dh!;
        subscription.Auth = auth!;
        subscription.DeviceName = WebPushSubscriptionRules.TrimToMaxLength(
            request.DeviceName,
            WebPushSubscriptionRules.MaxDeviceNameLength);
        subscription.UserAgent = WebPushSubscriptionRules.TrimToMaxLength(
            request.UserAgent,
            WebPushSubscriptionRules.MaxUserAgentLength);
        subscription.IsActive = true;
        subscription.LastFailureAt = null;
        subscription.FailureCount = 0;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<WebPushSubscriptionDto>.Ok(new WebPushSubscriptionDto
        {
            SubscriptionId = subscription.WebPushSubscriptionId,
            DeviceName = subscription.DeviceName,
            IsActive = subscription.IsActive,
            CreatedAt = subscription.CreatedAt
        });
    }
}
