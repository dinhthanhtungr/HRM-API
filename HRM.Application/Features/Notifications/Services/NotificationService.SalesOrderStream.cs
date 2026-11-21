using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

internal sealed partial class NotificationService
{
    private const string SalesOrderStreamKey = "sales-order-stream";
    private const string DeliveryDateStreamKey = "sales-order-delivery-stream";
    private const string DeliveryStatusStreamKey = "sales-order-delivery-status-stream";

    private IQueryable<NotificationDto> SalesOrderStreamQuery(string streamKey = SalesOrderStreamKey)
    {
        var company = GetCurrentCompanyId();
        var employee = GetCurrentEmployeeId();
        var topics = streamKey switch
        {
            DeliveryDateStreamKey => new[] { TopicNotifications.MerchandiseOrderPromisedDeliveryNotified },
            DeliveryStatusStreamKey => new[] { TopicNotifications.MerchandiseOrderDeliveryPaused, TopicNotifications.MerchandiseOrderDeliveryResumed },
            _ => new[] { TopicNotifications.MerchandiseOrderBelowStandardPriceApproved }
        };
        // Luồng chung chỉ là cách đọc inbox; không tạo membership hoặc mở rộng người nhận cảnh báo giá.
        var topicQuery = streamKey == DeliveryStatusStreamKey
            ? NotificationDeliveryStatusRules.Filter(_dbContext.Notifications.AsNoTracking())
            : _dbContext.Notifications.AsNoTracking().Where(n => topics.Contains(n.Topic));
        return _areas.StandaloneNotifications(topicQuery).Where(n =>
            n.CompanyId == company &&
            n.CreatedDate >= NotificationTopicCategoryRules.CurrentDataStartDate &&
            n.UserStates.Any(s => s.UserId == employee && !s.IsArchived))
            .Select(n => new NotificationDto {
                Id = n.Id, Topic = n.Topic, Severity = n.Severity, Title = n.Title,
                Message = n.Message, Link = n.Link, PayloadJson = n.PayloadJson,
                CreatedDate = n.CreatedDate, CompanyId = n.CompanyId, CreatedBy = n.CreatedBy,
                CreatedByNameSnapshot = n.CreatedByNameSnapshot,
                IsRead = n.UserStates.Where(s => s.UserId == employee).Select(s => s.IsRead).FirstOrDefault(),
                ReadDate = n.UserStates.Where(s => s.UserId == employee).Select(s => s.ReadDate).FirstOrDefault()
            });
    }

    private async Task ApplySalesOrderStreamMetadataAsync(List<NotificationDto> items, CancellationToken ct)
    {
        var candidates = items.Where(n => n.ConversationId is null &&
            n.CategoryCode == NotificationCategoryCodes.SalesOrder &&
            (n.Topic is TopicNotifications.MerchandiseOrderBelowStandardPriceApproved or
                TopicNotifications.MerchandiseOrderPromisedDeliveryNotified or
                TopicNotifications.MerchandiseOrderDeliveryPaused or TopicNotifications.MerchandiseOrderDeliveryResumed ||
                n.Topic == TopicNotifications.MerchandiseOrderUpdated && NotificationDeliveryStatusRules.IsLegacyPayload(n.PayloadJson))).ToArray();
        if (candidates.Length == 0) return;
        foreach (var group in candidates.GroupBy(n => n.Topic == TopicNotifications.MerchandiseOrderPromisedDeliveryNotified
                     ? DeliveryDateStreamKey : n.Topic is TopicNotifications.MerchandiseOrderDeliveryPaused or TopicNotifications.MerchandiseOrderDeliveryResumed or TopicNotifications.MerchandiseOrderUpdated
                     ? DeliveryStatusStreamKey : SalesOrderStreamKey))
        {
            var unread = await SalesOrderStreamQuery(group.Key).CountAsync(n => !n.IsRead, ct);
            foreach (var item in group)
            {
                item.StreamKey = group.Key;
                item.StreamUnreadCount = unread;
            }
        }
    }

    public async Task<NotificationStreamPageDto> GetSalesOrderStreamAsync(
        Guid? cursorId = null, string direction = "initial", CancellationToken cancellationToken = default,
        string streamKey = SalesOrderStreamKey)
    {
        if (direction is not ("initial" or "older" or "newer") || (direction != "initial" && cursorId is null))
            throw new ArgumentException("Invalid stream cursor or direction.");
        if (streamKey is not (SalesOrderStreamKey or DeliveryDateStreamKey or DeliveryStatusStreamKey))
            throw new ArgumentException("Invalid notification stream.");
        var query = SalesOrderStreamQuery(streamKey);
        var unread = await query.CountAsync(n => !n.IsRead, cancellationToken);
        var firstUnread = await query.Where(n => !n.IsRead).OrderBy(n => n.CreatedDate).ThenBy(n => n.Id)
            .Select(n => new { n.Id, n.CreatedDate }).FirstOrDefaultAsync(cancellationToken);
        var anchor = cursorId.HasValue
            ? await query.Where(n => n.Id == cursorId.Value).Select(n => new { n.Id, n.CreatedDate })
                .FirstOrDefaultAsync(cancellationToken)
            : firstUnread;
        // Một cursor bị thu hồi/khác công ty không được dùng để tra thời gian hoặc nội dung.
        if (cursorId.HasValue && anchor is null) return new([], firstUnread?.Id, unread, false, false);
        var page = query;
        var ascending = direction == "newer" || (direction == "initial" && anchor is not null);
        if (anchor is not null)
        {
            var date = anchor.CreatedDate;
            var id = anchor.Id;
            page = direction == "older"
                ? page.Where(n => n.CreatedDate < date || (n.CreatedDate == date && n.Id.CompareTo(id) < 0))
                : direction == "newer"
                    ? page.Where(n => n.CreatedDate > date || (n.CreatedDate == date && n.Id.CompareTo(id) > 0))
                    : page.Where(n => n.CreatedDate > date || (n.CreatedDate == date && n.Id.CompareTo(id) >= 0));
        }
        var items = await (ascending ? page.OrderBy(n => n.CreatedDate).ThenBy(n => n.Id)
            : page.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id))
            .Take(30).ToListAsync(cancellationToken);
        if (!ascending) items.Reverse();
        foreach (var item in items) NotificationPayloadPresentation.Apply(item);
        await ApplySalesOrderDetailsAsync(items, cancellationToken);
        await _areas.MaskPriceNotificationsAsync(items, cancellationToken);
        var older = false;
        var newer = false;
        if (items.Count > 0)
        {
            var first = items[0];
            var last = items[^1];
            older = await query.AnyAsync(n => n.CreatedDate < first.CreatedDate ||
                (n.CreatedDate == first.CreatedDate && n.Id.CompareTo(first.Id) < 0), cancellationToken);
            newer = await query.AnyAsync(n => n.CreatedDate > last.CreatedDate ||
                (n.CreatedDate == last.CreatedDate && n.Id.CompareTo(last.Id) > 0), cancellationToken);
        }
        return new(items, firstUnread?.Id, unread, older, newer);
    }
}
