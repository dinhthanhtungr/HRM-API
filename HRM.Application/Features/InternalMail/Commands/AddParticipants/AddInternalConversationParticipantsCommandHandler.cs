using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.AddParticipants;

internal sealed class AddInternalConversationParticipantsCommandHandler
    : IRequestHandler<AddInternalConversationParticipantsCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AddInternalConversationParticipantsCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(
        AddInternalConversationParticipantsCommand request,
        CancellationToken cancellationToken)
    {
        var employeeIds = request.EmployeeIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (request.ConversationId == Guid.Empty || employeeIds.Length == 0)
        {
            return OperationResult.Fail("At least one employee is required.");
        }

        // API nay khong cho tu nang quyen Owner; quyen Owner can mot use case chuyen giao rieng.
        if (request.Role == InternalConversationParticipantRole.Owner)
        {
            return OperationResult.Fail("Owner role cannot be assigned by this endpoint.");
        }

        var companyId = _currentUser.CompanyId;
        var actorId = _currentUser.EmployeeId;
        if (!companyId.HasValue || !actorId.HasValue)
        {
            return OperationResult.Fail("Current employee or company is invalid.");
        }

        var actorIsOwner = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == actorId.Value &&
                x.Role == InternalConversationParticipantRole.Owner &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        if (!actorIsOwner)
        {
            return OperationResult.Fail("Only the conversation owner can add participants.");
        }

        var validEmployeeIds = await _dbContext.Employees
            .AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.CompanyId == companyId.Value && x.IsActive)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);
        if (validEmployeeIds.Count != employeeIds.Length)
        {
            return OperationResult.Fail("Some employees do not exist or are inactive.");
        }

        var existingIds = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => x.InternalConversationId == request.ConversationId && employeeIds.Contains(x.EmployeeId))
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);
        var now = _dateTimeProvider.Now;

        foreach (var employeeId in validEmployeeIds.Except(existingIds))
        {
            await _dbContext.InternalConversationParticipants.AddAsync(new InternalConversationParticipant
            {
                InternalConversationId = request.ConversationId,
                EmployeeId = employeeId,
                Role = request.Role,
                JoinedAt = now
            }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
