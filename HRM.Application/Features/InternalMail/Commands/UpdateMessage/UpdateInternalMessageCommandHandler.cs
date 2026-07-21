using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.UpdateMessage;

internal sealed class UpdateInternalMessageCommandHandler
    : IRequestHandler<UpdateInternalMessageCommand, OperationResult>
{
    private const int MaxBodyLength = 2000;
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateInternalMessageCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(UpdateInternalMessageCommand request, CancellationToken cancellationToken)
    {
        if (request.MessageId == Guid.Empty || (request.Body is null && !request.IsUrgent.HasValue))
        {
            return OperationResult.Fail("At least one field must be supplied.");
        }

        var body = request.Body?.Trim();
        if (request.Body is not null && (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength))
        {
            return OperationResult.Fail($"Body cannot be empty or exceed {MaxBodyLength} characters.");
        }

        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (!companyId.HasValue || !employeeId.HasValue)
        {
            return OperationResult.Fail("Current employee or company is invalid.");
        }

        var message = await _dbContext.InternalMessages
            .FirstOrDefaultAsync(x =>
                x.InternalMessageId == request.MessageId &&
                x.SenderEmployeeId == employeeId.Value &&
                !x.IsDeleted &&
                x.MessageType != InternalMessageType.System &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive &&
                x.Conversation.Participants.Any(participant => participant.EmployeeId == employeeId.Value),
                cancellationToken);

        if (message is null)
        {
            return OperationResult.Fail("Message was not found or cannot be edited.");
        }

        if (request.Body is not null)
        {
            message.Body = body!;
        }

        if (request.IsUrgent.HasValue)
        {
            message.IsUrgent = request.IsUrgent.Value;
        }

        message.IsEdited = true;
        message.EditedAt = _dateTimeProvider.Now;
        message.EditedByEmployeeId = employeeId.Value;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Ok("Message updated successfully.");
    }
}
