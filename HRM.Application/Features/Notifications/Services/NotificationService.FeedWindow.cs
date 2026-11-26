using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

internal sealed partial class NotificationService
{
    private async Task<InternalMailAreaAccessService.InboxNotificationReadScope> PlainFeedWindowAsync(
        IQueryable<Notification> inbox, int take, Guid? afterId, DateTime? afterCreated,
        string? categoryCode, string? eventGroupCode, CancellationToken ct)
    {
        var category = NotificationTopicCatalog.NormalizeCode(categoryCode);
        var eventGroup = NotificationTopicCatalog.NormalizeCode(eventGroupCode);
        var query = inbox;
        if (category == NotificationCategoryCodes.LegacyData)
            query = query.Where(n => n.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate);
        else if (category is not null || eventGroup is not null)
        {
            var topics = NotificationTopicCatalog.GetTopics(category, eventGroup);
            query = query.Where(n => n.CreatedDate >= NotificationTopicCategoryRules.CurrentDataStartDate && topics.Contains(n.Topic));
        }
        var selected = new List<Guid>();
        var cursorId = afterId;
        var cursorAt = afterCreated;
        var windowSize = Math.Max(64, take * 2);
        // Chỉ tối ưu timeline thường. Hub latest/unread/@Tôi vẫn giữ winner/filter
        // nghiệp vụ trước phân trang. Không trả trang thiếu vì loại các dòng mất quyền.
        for (var window = 0; window < 8; window++)
        {
            var candidates = query;
            if (cursorId.HasValue && cursorAt.HasValue)
            {
                var id = cursorId.Value;
                var at = cursorAt.Value;
                candidates = candidates.Where(n => n.CreatedDate < at || (n.CreatedDate == at && n.Id.CompareTo(id) < 0));
            }
            var batch = await candidates.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
                .Select(n => new { n.Id, n.CreatedDate }).Take(windowSize).ToArrayAsync(ct);
            if (batch.Length == 0) break;
            var allowed = (await _areas.AuthorizeInboxEventIdsAsync(batch.Select(n => n.Id).ToArray(), true, ct)).ToHashSet();
            selected.AddRange(batch.Where(n => allowed.Contains(n.Id)).Select(n => n.Id).Take(take - selected.Count));
            if (selected.Count == take || batch.Length < windowSize) break;
            cursorId = batch[^1].Id;
            cursorAt = batch[^1].CreatedDate;
            if (window == 7)
                // Nhiều dòng bị ẩn: dùng query gốc thay vì scan từng cửa sổ quá lâu.
                return await _areas.DefaultLabInboxSelectionAsync(inbox, ct, excludeReactions: true);
        }
        var ids = selected.ToArray();
        return new(inbox.Where(n => ids.Contains(n.Id)), ids,
            GetCurrentCompanyId(), GetCurrentEmployeeId(), _currentUser.UserId);
    }
}
