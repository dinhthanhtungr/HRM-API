using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.Notifications;
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
    private readonly InternalMailAreaAccessService _areas;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly InternalMailMentionResolver _mentions;
    private readonly INotificationService _notifications;

    public UpdateInternalMessageCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        InternalMailAreaAccessService areas,
        InternalMailMentionResolver mentions,
        INotificationService notifications)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _dateTimeProvider = dateTimeProvider;
        _mentions = mentions;
        _notifications = notifications;
    }

    public async Task<OperationResult> Handle(UpdateInternalMessageCommand request, CancellationToken cancellationToken)
    {
        if (request.MessageId == Guid.Empty || (request.Body is null && !request.IsUrgent.HasValue && request.MentionedEmployeeIds is null))
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

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var conversationId = await _areas.Messages().AsNoTracking()
            .Where(m => m.InternalMessageId == request.MessageId && m.SenderEmployeeId == employeeId.Value)
            .Select(m => (Guid?)m.InternalConversationId).FirstOrDefaultAsync(cancellationToken);
        if (!conversationId.HasValue) return OperationResult.Fail("Message was not found or cannot be edited.");
        await _dbContext.LockInternalConversationAsync(companyId.Value, conversationId.Value, cancellationToken);
        var message = await _areas.Messages()
            .FirstOrDefaultAsync(x =>
                x.InternalMessageId == request.MessageId &&
                x.SenderEmployeeId == employeeId.Value &&
                !x.IsDeleted &&
                x.MessageType != InternalMessageType.System &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive &&
                x.Conversation.Participants.Any(participant =>
                    participant.EmployeeId == employeeId.Value && participant.IsActive),
                cancellationToken);

        if (message is null)
        {
            return OperationResult.Fail("Message was not found or cannot be edited.");
        }

        if (QuotationSendService.ReadPayload(message.PayloadJson) is not null)
            return OperationResult.Fail("Quotation confirmation messages are managed by the quotation workflow.");

        var newMentionIds = Array.Empty<Guid>();
        if (request.MentionedEmployeeIds is not null)
        {
            var resolved = await _mentions.ResolveAsync(companyId.Value, message.InternalConversationId,
                request.MentionedEmployeeIds, cancellationToken);
            if (!resolved.Success) return OperationResult.Fail(resolved.Message!);
            var notified = InternalMailMentions.Notified(message.PayloadJson);
            newMentionIds = resolved.Data!.Select(m => m.EmployeeId)
                .Where(id => id != employeeId.Value && !notified.Contains(id)).ToArray();
            if (resolved.Data!.Count > 0 || InternalMailMentions.Read(message.PayloadJson).Count > 0)
            {
                try
                {
                    message.PayloadJson = InternalMailMentions.Write(message.PayloadJson, resolved.Data!, notified, markAudience: false);
                }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
                {
                    return OperationResult.Fail("This historical message has unsupported metadata and cannot be tagged.");
                }
            }
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

        if (newMentionIds.Length > 0)
        {
            var conversation = await _dbContext.InternalConversations.AsNoTracking()
                .SingleAsync(c => c.InternalConversationId == message.InternalConversationId && c.CompanyId == companyId, cancellationToken);
            var rootId = await _areas.RootIdAsync(conversation.InternalConversationId, cancellationToken);
            var context = rootId != conversation.InternalConversationId
                ? await _dbContext.InternalConversations.AsNoTracking().SingleAsync(c =>
                    c.InternalConversationId == rootId && c.CompanyId == companyId && c.IsActive, cancellationToken)
                : conversation;
            await _notifications.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = companyId.Value,
                CreatedBy = employeeId.Value,
                UseExactAudience = true,
                MentionedEmployeeIds = newMentionIds,
                TargetUserIds = newMentionIds,
                Topic = context.RelatedType switch
                {
                    InternalMailRelatedType.SampleRequest => TopicNotifications.SampleRequestMessageCreated,
                    InternalMailRelatedType.Quotation => TopicNotifications.QuotationMessageCreated,
                    _ => TopicNotifications.InternalMailMessageCreated
                },
                Title = "Bạn được nhắc tên trong cuộc trao đổi",
                Message = message.Body,
                ConversationId = message.InternalConversationId,
                MessageId = message.InternalMessageId,
                AggregateId = context.RelatedId,
                AggregateCode = context.RelatedExternalId,
                PayloadJson = message.PayloadJson,
                Link = $"/internal-mail/conversations/{message.InternalConversationId}"
            }, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return OperationResult.Ok("Message updated successfully.");
    }
}
