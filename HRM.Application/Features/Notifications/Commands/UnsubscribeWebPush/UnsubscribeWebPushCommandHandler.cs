using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Commands.UnsubscribeWebPush;

internal sealed class UnsubscribeWebPushCommandHandler
    : IRequestHandler<UnsubscribeWebPushCommand, OperationResult>
{
    private readonly INotificationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public UnsubscribeWebPushCommandHandler(
        INotificationDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult> Handle(
        UnsubscribeWebPushCommand request,
        CancellationToken cancellationToken)
    {
        var endpoint = request.Endpoint?.Trim();
        if (!WebPushSubscriptionRules.IsValidEndpoint(endpoint))
        {
            return OperationResult.Fail("A valid HTTPS push endpoint is required.");
        }

        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (!companyId.HasValue || !employeeId.HasValue)
        {
            return OperationResult.Fail("Current employee or company is invalid.");
        }

        var updated = await _dbContext.WebPushSubscriptions
            .Where(x =>
                x.Endpoint == endpoint &&
                x.CompanyId == companyId.Value &&
                x.EmployeeId == employeeId.Value &&
                x.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);

        return updated > 0
            ? OperationResult.Ok()
            : OperationResult.Fail("Web Push subscription was not found.");
    }
}
