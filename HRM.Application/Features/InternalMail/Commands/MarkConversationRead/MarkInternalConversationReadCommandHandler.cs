using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.MarkConversationRead;

internal sealed class MarkInternalConversationReadCommandHandler
    : IRequestHandler<MarkInternalConversationReadCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMailAreaAccessService _areas;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MarkInternalConversationReadCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        InternalMailAreaAccessService areas)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(MarkInternalConversationReadCommand request, CancellationToken cancellationToken)
    {
        using var timing = new NotificationQueryTiming(null, "thread-read-write",
            System.Diagnostics.Activity.Current?.GetTagItem(NotificationDiagnosticTags.ThreadTiming) is "enabled",
            "read-write-total");
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return OperationResult.Fail("Conversation or current user is invalid.");
        }

        var conversation = await timing.RunAsync("read-access", () =>
            _areas.ConversationsForCandidates([request.ConversationId]).AsNoTracking()
                .FirstOrDefaultAsync(c => c.InternalConversationId == request.ConversationId, cancellationToken));
        if (conversation is null || (request.AreaCode != null &&
            request.AreaCode != InternalMailAreaAccessService.AreaOf(conversation.RelatedType)))
            return OperationResult.Fail("Conversation area was not found.");

        // Giờ xử lý dùng cho audit; khi có ID mốc, thứ tự message lấy từ dữ liệu đã lưu.
        var readThrough = _dateTimeProvider.Now;
        var inboxScope = await timing.RunAsync("read-inbox-scope", () =>
            _areas.UnreadCandidateStatesAsync(request.ConversationId, cancellationToken));
        var inbox = inboxScope.Query;
        // Snapshot thread-only events before acknowledging messages. Their persisted
        // timestamps may be ahead of this API clock; incoming rows after this snapshot
        // must remain unread even when their timestamps are backdated.
        var threadNotificationIds = request.ThroughMessageId.HasValue
            ? await timing.RunAsync("read-event-snapshot", () => (from state in inbox
                     join link in _dbContext.QueryInternalMailNotificationLinks(companyId.Value)
                         .Where(l => inboxScope.EventIds.Contains(l.NotificationId))
                         on state.NotificationId equals link.NotificationId
                     where link.ConversationId == request.ConversationId && !link.MessageId.HasValue &&
                         state.UserId == employeeId.Value && !state.IsRead && !state.IsArchived &&
                         state.Notification.CompanyId == companyId.Value &&
                         state.Notification.Topic == HRM.Domain.Enums.Notifications.TopicNotifications.QuotationPricingApproved
                     select state.NotificationId).ToArrayAsync(cancellationToken))
            : [];
        var participants = _dbContext.InternalConversationParticipants
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive);
        var isParticipant = await timing.RunAsync("read-membership", () => participants.AnyAsync(cancellationToken));
        // Conversations() đã xác thực quyền đọc (participant hoặc observer đúng phạm vi).
        // Mọi reader hợp lệ có thể acknowledge inbox của chính mình, không cần cấp membership.

        var messageReadThrough = readThrough;
        if (request.ThroughMessageId.HasValue)
        {
            var boundary = await timing.RunAsync("read-boundary", () => _areas.MessagesForCandidates([request.ConversationId], request.AreaCode).AsNoTracking()
                .Where(InternalConversationReadScope.Boundary(
                    companyId.Value, request.ConversationId, request.ThroughMessageId.Value))
                .Select(message => new { message.SentAt })
                .SingleOrDefaultAsync(cancellationToken));
            if (boundary is null)
            {
                return OperationResult.Fail("Read boundary message was not found.");
            }

            messageReadThrough = boundary.SentAt;
        }

        var messageScope = _areas.MessagesForCandidates([request.ConversationId], request.AreaCode)
            .Where(InternalConversationReadScope.ConversationMessages(
                companyId.Value, employeeId.Value, request.ConversationId,
                messageReadThrough, request.ThroughMessageId,
                allowCompanyObserver: _areas.CanObserveCompanyConversations));
        var unreadLinks = from state in inbox
                          join link in _dbContext.QueryInternalMailNotificationLinks(companyId.Value)
                              .Where(l => inboxScope.EventIds.Contains(l.NotificationId))
                              on state.NotificationId equals link.NotificationId
                          where link.ConversationId == request.ConversationId &&
                              state.UserId == employeeId.Value && !state.IsRead && !state.IsArchived &&
                              state.Notification.CompanyId == companyId.Value
                          select link;
        // Legacy GUID formats still use the existing payload parser. Only that rare
        // path requires all historical IDs; canonical inbox links stay in SQL.
        var hasLegacyMessageLinks = await timing.RunAsync("read-legacy-check", () => unreadLinks.AnyAsync(
            link => !link.MessageId.HasValue && link.HasMessageValue, cancellationToken));
        var notificationMessageIds = unreadLinks.Where(link => link.MessageId.HasValue)
            .Select(link => link.MessageId!.Value);
        var messageIds = await timing.RunAsync("read-message-snapshot", () => messageScope.AsNoTracking()
            .Where(message => hasLegacyMessageLinks ||
                (isParticipant && !message.ReadStates.Any(state => state.EmployeeId == employeeId.Value && state.IsRead)) ||
                notificationMessageIds.Contains(message.InternalMessageId))
            .Select(message => message.InternalMessageId)
            .ToArrayAsync(cancellationToken));

        await using var transaction = await timing.RunAsync("read-begin", () => _dbContext.BeginTransactionAsync(cancellationToken));
        // Observer không có membership: chỉ acknowledge inbox của chính họ, không tạo reader/member mới.
        // Snapshot ID ngăn message xuất hiện giữa các query bị đánh dấu ngoài ý muốn.
        if (isParticipant)
            await timing.RunAsync("read-messages", () => _dbContext.UpsertInternalMessageReadStatesAsync(
                companyId.Value, employeeId.Value, request.ConversationId, messageIds, readThrough, cancellationToken));
        await timing.RunAsync("read-notifications", () => MarkNotificationsReadAsync(
            companyId.Value, employeeId.Value, request.ConversationId,
            readThrough, request.ThroughMessageId.HasValue, hasLegacyMessageLinks,
            messageIds.ToHashSet(), threadNotificationIds.ToHashSet(), inboxScope, timing, cancellationToken));

        // Request cũ hoàn tất sau request mới không được kéo LastReadAt lùi lại.
        if (isParticipant)
            await timing.RunAsync("read-participant", () => participants
                .Where(participant => participant.LastReadAt == null || participant.LastReadAt < messageReadThrough)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastReadAt, messageReadThrough), cancellationToken));
        await timing.RunAsync("read-commit", () => transaction.CommitAsync(cancellationToken));
        return OperationResult.Ok();
    }

    private async Task MarkNotificationsReadAsync(
        Guid companyId, Guid employeeId, Guid conversationId, DateTime readThrough, bool hasMessageBoundary,
        bool hasLegacyMessageLinks,
        IReadOnlySet<Guid> messageIds, IReadOnlySet<Guid> threadNotificationIds,
        InternalMailAreaAccessService.CandidateNotificationStateScope inbox,
        NotificationQueryTiming timing, CancellationToken cancellationToken)
    {
        const int batchSize = 200;
        var scope = inbox.Query
            // Mốc ID đã giới hạn tập message. Không loại notification của chúng vì đồng hồ lệch.
            .Where(InternalConversationReadScope.Notifications(
                companyId, employeeId, hasMessageBoundary ? null : readThrough));
        var snapshotIds = messageIds.ToArray();
        var threadIds = threadNotificationIds.ToArray();
        var links = _dbContext.QueryInternalMailNotificationLinks(companyId)
            .Where(link => link.ConversationId == conversationId && inbox.EventIds.Contains(link.NotificationId));
        var canonicalIds = links.Where(link =>
                (link.MessageId.HasValue && snapshotIds.Contains(link.MessageId.Value)) ||
                (!link.HasMessageValue && (hasMessageBoundary
                    ? threadIds.Contains(link.NotificationId)
                    : scope.Any(state => state.NotificationId == link.NotificationId &&
                        state.Notification.Topic == HRM.Domain.Enums.Notifications.TopicNotifications.QuotationPricingApproved))))
            .Select(link => link.NotificationId);
        // One set update for canonical links, including duplicate deliveries of the
        // same message. Keep the frozen message/thread snapshots and scoped inbox.
        await timing.RunAsync("read-inbox-update", () => scope.Where(state => canonicalIds.Contains(state.NotificationId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.IsRead, true)
                .SetProperty(state => state.ReadDate, readThrough), cancellationToken));
        // Tập canonical/legacy đã giới hạn conversation riêng. Không lặp lại phép
        // phân tích JSON của conversation trên outer scope; vẫn giữ scope quyền/inbox.
        // Snapshot đầu request không có legacy thì không quét lại lịch sử ở cuối.
        if (!hasLegacyMessageLinks) return;
        var legacyIds = links.Where(link => !link.MessageId.HasValue && link.HasMessageValue)
            .Select(link => link.NotificationId);
        scope = scope.Where(state => legacyIds.Contains(state.NotificationId));
        DateTime? afterCreated = null;
        Guid? afterId = null;

        // SQL limits batches to this conversation; retain the legacy parser for message/boundary semantics.
        await timing.RunAsync("read-inbox-legacy", async () =>
        {
            while (true)
            {
                var query = scope.AsNoTracking();
                if (afterCreated.HasValue && afterId.HasValue)
                {
                    var created = afterCreated.Value;
                    var id = afterId.Value;
                    query = query.Where(state => state.Notification.CreatedDate > created ||
                        (state.Notification.CreatedDate == created && state.NotificationId.CompareTo(id) > 0));
                }

                var batch = await query
                    .OrderBy(state => state.Notification.CreatedDate)
                    .ThenBy(state => state.NotificationId)
                    .Take(batchSize)
                    .Select(state => new NotificationDto
                    {
                        Id = state.NotificationId,
                        Topic = state.Notification.Topic,
                        PayloadJson = state.Notification.PayloadJson,
                        CreatedDate = state.Notification.CreatedDate
                    })
                    .ToListAsync(cancellationToken);
                if (batch.Count == 0)
                {
                    break;
                }

                var ids = batch
                    .Where(notification => InternalConversationReadScope.MatchesReadReceipt(
                        notification, conversationId, messageIds, readThrough,
                        hasMessageBoundary ? threadNotificationIds : null))
                    .Select(notification => notification.Id)
                    .ToArray();
                if (ids.Length > 0)
                {
                    await scope.Where(state => ids.Contains(state.NotificationId))
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(state => state.IsRead, true)
                            .SetProperty(state => state.ReadDate, readThrough), cancellationToken);
                }

                afterCreated = batch[^1].CreatedDate;
                afterId = batch[^1].Id;
                if (batch.Count < batchSize)
                {
                    break;
                }
            }
        });
    }
}
