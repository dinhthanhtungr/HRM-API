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
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return OperationResult.Fail("Conversation or current user is invalid.");
        }

        var conversation = await _areas.Conversations().AsNoTracking()
            .FirstOrDefaultAsync(c => c.InternalConversationId == request.ConversationId, cancellationToken);
        if (conversation is null || (request.AreaCode != null &&
            request.AreaCode != InternalMailAreaAccessService.AreaOf(conversation.RelatedType)))
            return OperationResult.Fail("Conversation area was not found.");

        // Giờ xử lý dùng cho audit; khi có ID mốc, thứ tự message lấy từ dữ liệu đã lưu.
        var readThrough = _dateTimeProvider.Now;
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var participants = _dbContext.InternalConversationParticipants
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive);
        if (!await participants.AnyAsync(cancellationToken))
        {
            return OperationResult.Fail("Conversation was not found.");
        }

        var messageReadThrough = readThrough;
        if (request.ThroughMessageId.HasValue)
        {
            var boundary = await _areas.Messages(request.AreaCode).AsNoTracking()
                .Where(InternalConversationReadScope.Boundary(
                    companyId.Value, request.ConversationId, request.ThroughMessageId.Value))
                .Select(message => new { message.SentAt })
                .SingleOrDefaultAsync(cancellationToken);
            if (boundary is null)
            {
                return OperationResult.Fail("Read boundary message was not found.");
            }

            messageReadThrough = boundary.SentAt;
        }

        var messageScope = _areas.Messages(request.AreaCode)
            .Where(InternalConversationReadScope.ConversationMessages(
                companyId.Value, employeeId.Value, request.ConversationId,
                messageReadThrough, request.ThroughMessageId));
        var messageIds = await messageScope.AsNoTracking()
            .Select(message => message.InternalMessageId)
            .ToArrayAsync(cancellationToken);

        // Snapshot ID ngăn message xuất hiện giữa các query bị đánh dấu ngoài ý muốn.
        await _dbContext.UpsertInternalMessageReadStatesAsync(
            companyId.Value, employeeId.Value, request.ConversationId, messageIds, readThrough, cancellationToken);
        await MarkNotificationsReadAsync(
            companyId.Value, employeeId.Value, request.ConversationId,
            readThrough, request.ThroughMessageId.HasValue, messageIds.ToHashSet(), request.AreaCode, cancellationToken);

        // Request cũ hoàn tất sau request mới không được kéo LastReadAt lùi lại.
        await participants
            .Where(participant => participant.LastReadAt == null || participant.LastReadAt < messageReadThrough)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastReadAt, messageReadThrough), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok();
    }

    private async Task MarkNotificationsReadAsync(
        Guid companyId, Guid employeeId, Guid conversationId, DateTime readThrough, bool hasMessageBoundary,
        IReadOnlySet<Guid> messageIds, string? areaCode, CancellationToken cancellationToken)
    {
        const int batchSize = 200;
        var scope = _areas.States()
            // Mốc ID đã giới hạn tập message. Không loại notification của chúng vì đồng hồ lệch.
            .Where(InternalConversationReadScope.Notifications(
                companyId, employeeId, hasMessageBoundary ? null : readThrough));
        DateTime? afterCreated = null;
        Guid? afterId = null;

        // conversationId/messageId nằm trong JSON lịch sử. Duyệt có giới hạn theo batch,
        // không tìm chuỗi trên jsonb, không chỉ xử lý trang notification FE đang hiển thị.
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
                    notification, conversationId, messageIds, readThrough))
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
    }
}
