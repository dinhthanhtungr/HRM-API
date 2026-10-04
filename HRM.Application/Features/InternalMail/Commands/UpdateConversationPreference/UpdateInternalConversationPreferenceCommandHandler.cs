using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.UpdateConversationPreference;

internal sealed class UpdateInternalConversationPreferenceCommandHandler
    : IRequestHandler<UpdateInternalConversationPreferenceCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HRM.Application.Abstractions.Notifications.INotificationInboxArchiver _inboxArchiver;

    public UpdateInternalConversationPreferenceCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        HRM.Application.Abstractions.Notifications.INotificationInboxArchiver inboxArchiver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _inboxArchiver = inboxArchiver;
    }

    public async Task<OperationResult> Handle(
        UpdateInternalConversationPreferenceCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ConversationId == Guid.Empty || (!request.IsArchived.HasValue && !request.IsMuted.HasValue))
        {
            return OperationResult.Fail("At least one preference must be supplied.");
        }

        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (!companyId.HasValue || !employeeId.HasValue)
        {
            return OperationResult.Fail("Current employee or company is invalid.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var participant = await _dbContext.InternalConversationParticipants
            .FirstOrDefaultAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        if (participant is null)
        {
            return OperationResult.Fail("Conversation was not found.");
        }

        if (request.IsArchived.HasValue)
        {
            participant.IsArchived = request.IsArchived.Value;
            participant.ArchivedAt = request.IsArchived.Value ? _dateTimeProvider.Now : null;
        }

        if (request.IsMuted.HasValue)
        {
            participant.IsMuted = request.IsMuted.Value;
        }

        if (request.IsArchived == true)
        {
            await _inboxArchiver.ArchiveConversationAsync(companyId.Value, employeeId.Value,
                request.ConversationId, cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
