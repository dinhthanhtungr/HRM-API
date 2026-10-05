using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.DeleteMessage;

internal sealed class DeleteInternalMessageCommandHandler
    : IRequestHandler<DeleteInternalMessageCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMessageDeletionAccess _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HRM.Application.Abstractions.Notifications.INotificationInboxArchiver _inboxArchiver;

    public DeleteInternalMessageCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        HRM.Application.Abstractions.Notifications.INotificationInboxArchiver inboxArchiver,
        ICurrentUserPermissionService permissions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _access = new InternalMessageDeletionAccess(currentUser, permissions);
        _dateTimeProvider = dateTimeProvider;
        _inboxArchiver = inboxArchiver;
    }

    public async Task<OperationResult> Handle(DeleteInternalMessageCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.MessageId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return OperationResult.Fail("Message or current user is invalid.");
        }

        if (!_access.CanDelete)
        {
            return OperationResult.Fail("Only Developer with message deletion permission can delete messages.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var message = await _access.Scope(_dbContext.InternalMessages)
            .FirstOrDefaultAsync(x =>
                x.InternalMessageId == request.MessageId,
                cancellationToken);

        if (message is null)
        {
            return OperationResult.Fail("Message was not found or cannot be deleted.");
        }

        var now = _dateTimeProvider.Now;
        message.IsDeleted = true;
        message.DeletedAt = now;
        message.DeletedByEmployeeId = employeeId.Value;

        var conversation = await _dbContext.InternalConversations
            .FirstAsync(x => x.InternalConversationId == message.InternalConversationId, cancellationToken);
        if (conversation.LastMessageId == message.InternalMessageId)
        {
            var previous = await _dbContext.InternalMessages
                .AsNoTracking()
                .Where(x =>
                    x.InternalConversationId == message.InternalConversationId &&
                    x.InternalMessageId != message.InternalMessageId &&
                    !x.IsDeleted)
                .OrderByDescending(x => x.SentAt)
                .ThenByDescending(x => x.InternalMessageId)
                .Select(x => new { x.InternalMessageId, x.SentAt })
                .FirstOrDefaultAsync(cancellationToken);

            conversation.LastMessageId = previous?.InternalMessageId;
            conversation.LastMessageAt = previous?.SentAt ?? conversation.CreatedAt;
        }

        await _inboxArchiver.ArchiveDeletedMessageAsync(companyId.Value, message.InternalConversationId,
            message.InternalMessageId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Message deleted successfully.");
    }
}
