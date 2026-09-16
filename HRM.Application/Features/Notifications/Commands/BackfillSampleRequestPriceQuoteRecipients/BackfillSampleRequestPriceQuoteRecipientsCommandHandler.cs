using System.Text.Json;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Commands.BackfillSampleRequestPriceQuoteRecipients;

internal sealed class BackfillSampleRequestPriceQuoteRecipientsCommandHandler
    : IRequestHandler<
        BackfillSampleRequestPriceQuoteRecipientsCommand,
        OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>>
{
    private const int MaximumRangeDays = 3;

    private readonly INotificationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public BackfillSampleRequestPriceQuoteRecipientsCommandHandler(
        INotificationDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>> Handle(
        BackfillSampleRequestPriceQuoteRecipientsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>.Fail(
                "Tài khoản hiện tại chưa được liên kết với công ty.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.Notifications.BackfillManagers))
        {
            return OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>.Fail(
                "Bạn không có quyền backfill notification Báo giá.");
        }

        var from = request.From ?? DateTime.Today.AddDays(-1);
        var to = request.To ?? DateTime.Today.AddDays(1);
        if (from >= to)
        {
            return OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>.Fail(
                "Khoảng thời gian backfill không hợp lệ.");
        }

        if (to - from > TimeSpan.FromDays(MaximumRangeDays))
        {
            return OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>.Fail(
                $"Khoảng thời gian backfill không được vượt quá {MaximumRangeDays} ngày.");
        }

        var sourceNotifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.CompanyId == companyId &&
                notification.Topic == TopicNotifications.SampleRequestPriceQuoteRequested &&
                notification.CreatedDate >= from &&
                notification.CreatedDate < to)
            .Select(notification => new SourceNotification(
                notification.Id,
                notification.CreatedBy,
                notification.PayloadJson))
            .ToListAsync(cancellationToken);

        var notificationReferences = sourceNotifications
            .Select(notification => new NotificationConversationReference(
                notification.NotificationId,
                notification.CreatedByEmployeeId,
                TryReadConversationId(notification.PayloadJson)))
            .ToArray();
        var conversationIds = notificationReferences
            .Select(reference => reference.ConversationId)
            .OfType<Guid>()
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var participants = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.IsActive &&
                conversationIds.Contains(participant.InternalConversationId) &&
                participant.Conversation.CompanyId == companyId &&
                participant.Conversation.IsActive &&
                participant.Conversation.RelatedType == InternalMailRelatedType.SampleRequest)
            .Select(participant => new ConversationParticipant(
                participant.InternalConversationId,
                participant.EmployeeId,
                participant.IsMuted))
            .ToListAsync(cancellationToken);
        var participantsByConversationId = participants
            .GroupBy(participant => participant.ConversationId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var notificationIds = sourceNotifications
            .Select(notification => notification.NotificationId)
            .ToArray();
        var existingRecipientKeys = await _dbContext.NotificationRecipients
            .AsNoTracking()
            .Where(recipient =>
                notificationIds.Contains(recipient.NotificationId) &&
                recipient.TargetUserId.HasValue)
            .Select(recipient => new NotificationEmployeeKey(
                recipient.NotificationId,
                recipient.TargetUserId!.Value))
            .ToListAsync(cancellationToken);
        var existingUserStateKeys = await _dbContext.NotificationUserStates
            .AsNoTracking()
            .Where(state => notificationIds.Contains(state.NotificationId))
            .Select(state => new NotificationEmployeeKey(state.NotificationId, state.UserId))
            .ToListAsync(cancellationToken);
        var recipientKeySet = existingRecipientKeys.ToHashSet();
        var userStateKeySet = existingUserStateKeys.ToHashSet();

        var candidates = new List<BackfillCandidate>();
        foreach (var reference in notificationReferences)
        {
            if (reference.ConversationId is not { } conversationId ||
                !participantsByConversationId.TryGetValue(conversationId, out var conversationParticipants))
            {
                continue;
            }

            foreach (var participant in conversationParticipants)
            {
                if (participant.EmployeeId == reference.CreatedByEmployeeId)
                {
                    continue;
                }

                var key = new NotificationEmployeeKey(reference.NotificationId, participant.EmployeeId);
                if (!recipientKeySet.Contains(key) || !userStateKeySet.Contains(key))
                {
                    candidates.Add(new BackfillCandidate(
                        key,
                        participant.IsMuted,
                        !recipientKeySet.Contains(key),
                        !userStateKeySet.Contains(key)));
                }
            }
        }

        var recipientRecordsCreated = candidates.Count(candidate => candidate.CreateRecipient);
        var userStatesCreated = candidates.Count(candidate => candidate.CreateUserState);
        var mutedUserStatesCreated = candidates.Count(candidate =>
            candidate.CreateUserState && candidate.IsMuted);

        if (!request.DryRun && candidates.Count > 0)
        {
            var now = DateTime.Now;
            foreach (var candidate in candidates)
            {
                if (candidate.CreateRecipient)
                {
                    await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
                    {
                        Id = Guid.CreateVersion7(),
                        NotificationId = candidate.Key.NotificationId,
                        TargetUserId = candidate.Key.EmployeeId
                    }, cancellationToken);
                }

                if (candidate.CreateUserState)
                {
                    await _dbContext.NotificationUserStates.AddAsync(new NotificationUserState
                    {
                        NotificationId = candidate.Key.NotificationId,
                        UserId = candidate.Key.EmployeeId,
                        IsRead = candidate.IsMuted,
                        ReadDate = candidate.IsMuted ? now : null,
                        IsArchived = false
                    }, cancellationToken);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = new BackfillSampleRequestPriceQuoteRecipientsResult(
            from,
            to,
            request.DryRun,
            sourceNotifications.Count,
            conversationIds.Length,
            notificationReferences.Count(reference => reference.ConversationId is null),
            candidates.Count,
            request.DryRun ? 0 : recipientRecordsCreated,
            request.DryRun ? 0 : userStatesCreated,
            request.DryRun ? 0 : mutedUserStatesCreated);

        return OperationResult<BackfillSampleRequestPriceQuoteRecipientsResult>.Ok(
            result,
            request.DryRun
                ? "Đã kiểm tra backfill notification Báo giá, chưa ghi dữ liệu."
                : "Đã bổ sung notification Báo giá cho participant còn thiếu.");
    }

    private static Guid? TryReadConversationId(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("conversationId", out var conversationIdElement) ||
                conversationIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return Guid.TryParse(conversationIdElement.GetString(), out var conversationId)
                ? conversationId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record SourceNotification(Guid NotificationId, Guid CreatedByEmployeeId, string? PayloadJson);

    private sealed record NotificationConversationReference(
        Guid NotificationId,
        Guid CreatedByEmployeeId,
        Guid? ConversationId);

    private sealed record ConversationParticipant(Guid ConversationId, Guid EmployeeId, bool IsMuted);

    private sealed record NotificationEmployeeKey(Guid NotificationId, Guid EmployeeId);

    private sealed record BackfillCandidate(
        NotificationEmployeeKey Key,
        bool IsMuted,
        bool CreateRecipient,
        bool CreateUserState);
}
