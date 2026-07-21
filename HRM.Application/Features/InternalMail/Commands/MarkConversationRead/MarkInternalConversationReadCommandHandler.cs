using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.MarkConversationRead;

internal sealed class MarkInternalConversationReadCommandHandler
    : IRequestHandler<MarkInternalConversationReadCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MarkInternalConversationReadCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(MarkInternalConversationReadCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return OperationResult.Fail("Conversation or current user is invalid.");
        }

        var participant = await _dbContext.InternalConversationParticipants
            .FirstOrDefaultAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        if (participant is null)
        {
            return OperationResult.Fail("Conversation was not found.");
        }

        var now = _dateTimeProvider.Now;
        await _dbContext.InternalMessageReadStates
            .Where(x =>
                x.EmployeeId == employeeId.Value &&
                !x.IsRead &&
                x.Message.InternalConversationId == request.ConversationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadAt, now),
                cancellationToken);

        participant.LastReadAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
