using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.NotificationHub;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.NotificationHub.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.NotificationHub.Commands.MarkAllRead;

internal sealed class MarkAllNotificationHubItemsReadCommandHandler
    : IRequestHandler<MarkAllNotificationHubItemsReadCommand, NotificationHubMarkAllReadResultDto>
{
    private readonly INotificationHubWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMailAreaAccessService _areas;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MarkAllNotificationHubItemsReadCommandHandler(
        INotificationHubWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        InternalMailAreaAccessService areas)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<NotificationHubMarkAllReadResultDto> Handle(
        MarkAllNotificationHubItemsReadCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user does not have a company.");
        var employeeId = _currentUser.EmployeeId
            ?? throw new UnauthorizedAccessException("Current user does not have an employee id.");
        var now = _dateTimeProvider.Now;

        await using var transaction = await _dbContext.BeginNotificationHubTransactionAsync(cancellationToken);

        var visibleIds = _areas.Messages().Select(m => m.InternalMessageId);
        var notificationsUpdated = await _areas.States()
            .Where(NotificationHubReadAllScope.UnreadNotifications(companyId, employeeId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(state => state.IsRead, true)
                .SetProperty(state => state.ReadDate, now),
                cancellationToken);

        // Cập nhật mốc conversation trước khi read-state chi tiết được bulk-update.
        var conversationsUpdated = await _dbContext.InternalConversationParticipants
            .Where(p => p.Conversation.Messages.Any(m => visibleIds.Contains(m.InternalMessageId)))
            .Where(NotificationHubReadAllScope.ConversationsWithUnreadMessages(companyId, employeeId, now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(participant => participant.LastReadAt, now),
                cancellationToken);

        var messagesUpdated = await _dbContext.InternalMessageReadStates
            .Where(s => visibleIds.Contains(s.InternalMessageId))
            .Where(NotificationHubReadAllScope.UnreadMessages(companyId, employeeId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(state => state.IsRead, true)
                .SetProperty(state => state.ReadAt, now),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new NotificationHubMarkAllReadResultDto
        {
            NotificationsUpdated = notificationsUpdated,
            MessagesUpdated = messagesUpdated,
            ConversationsUpdated = conversationsUpdated
        };
    }
}
