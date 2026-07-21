using System.Text.Json;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Notifications;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

/// <summary>
/// Lưu notification cho inbox API và đưa một gói SignalR nhỏ vào OutboxMessage.
/// Target user/role được resolve thành các dòng EmployeeId trong NotificationUserState.
/// </summary>
internal sealed class NotificationService : INotificationService
{
    private readonly INotificationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IWebPushSender _webPushSender;

    public NotificationService(
        INotificationDbContext dbContext,
        ICurrentUser currentUser,
        IWebPushSender webPushSender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _webPushSender = webPushSender;
    }

    public async Task<Guid> PublishAsync(
        PublishNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var companyId = request.CompanyId
            ?? _currentUser.CompanyId
            ?? throw new InvalidOperationException("CompanyId is required to publish notification.");

        var createdBy = request.CreatedBy
            ?? _currentUser.EmployeeId
            ?? _currentUser.UserId;

        if (createdBy == Guid.Empty)
        {
            throw new InvalidOperationException("CreatedBy is required to publish notification.");
        }

        var now = DateTime.Now;
        var notification = new Notification
        {
            Id = Guid.CreateVersion7(),
            Topic = request.Topic,
            Severity = request.Severity,
            Title = request.Title,
            Message = request.Message,
            Link = request.Link,
            PayloadJson = request.PayloadJson,
            CompanyId = companyId,
            CreatedBy = createdBy,
            CreatedByNameSnapshot = request.CreatedByNameSnapshot ?? _currentUser.UserName,
            CreatedDate = now
        };

        await _dbContext.Notifications.AddAsync(notification, cancellationToken);

        var targetUserIds = NormalizeIds(request.TargetUserIds);
        var targetRoles = NormalizeRoles(request.TargetRoles);
        var targetTeamIds = NormalizeIds(request.TargetTeamIds);

        // Recipient lưu lại ý định gửi ban đầu để audit/debug.
        // Việc user có thấy trong inbox hay không được quyết định bởi NotificationUserState bên dưới.
        foreach (var userId in targetUserIds)
        {
            await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
            {
                Id = Guid.CreateVersion7(),
                NotificationId = notification.Id,
                TargetUserId = userId
            }, cancellationToken);
        }

        foreach (var role in targetRoles)
        {
            await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
            {
                Id = Guid.CreateVersion7(),
                NotificationId = notification.Id,
                TargetRole = role
            }, cancellationToken);
        }

        foreach (var teamId in targetTeamIds)
        {
            await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
            {
                Id = Guid.CreateVersion7(),
                NotificationId = notification.Id,
                TargetTeamId = teamId
            }, cancellationToken);
        }

        var resolvedEmployeeIds = await ResolveEmployeeIdsAsync(
            companyId,
            targetUserIds,
            targetRoles,
            cancellationToken);

        // Tạo sẵn một dòng state cho mỗi nhân viên để query feed/unread đơn giản và nhanh.
        foreach (var employeeId in resolvedEmployeeIds)
        {
            await _dbContext.NotificationUserStates.AddAsync(new NotificationUserState
            {
                NotificationId = notification.Id,
                UserId = employeeId,
                IsRead = false,
                ReadDate = null,
                IsArchived = false
            }, cancellationToken);
        }

        // SignalR đi qua outbox để DB vẫn là nguồn dữ liệu chính.
        // Payload realtime chỉ chứa notificationId; FE tự gọi API để reload detail/feed.
        await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
        {
            Type = NotificationOutboxTypes.InAppPush,
            PayloadJson = JsonSerializer.Serialize(new OutboxEnvelope
            {
                CompanyId = companyId,
                NotificationId = notification.Id,
                TargetUserIds = targetUserIds,
                TargetRoles = targetRoles,
                TargetTeamIds = targetTeamIds
            }),
            CreatedAt = now
        }, cancellationToken);

        if (_webPushSender.IsEnabled && resolvedEmployeeIds.Count > 0)
        {
            await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
            {
                Type = NotificationOutboxTypes.WebPush,
                PayloadJson = JsonSerializer.Serialize(new WebPushOutboxPayload
                {
                    NotificationId = notification.Id
                }),
                CreatedAt = now
            }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return notification.Id;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetFeedAsync(
        int take = 20,
        Guid? afterId = null,
        DateTime? afterCreated = null,
        NotificationCategory category = NotificationCategory.All,
        CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();
        var normalizedTake = Math.Clamp(take, 1, 100);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.UserStates.Any(state => state.UserId == employeeId && !state.IsArchived))
            .Select(x => new NotificationDto
            {
                Id = x.Id,
                Topic = x.Topic,
                Severity = x.Severity,
                Title = x.Title,
                Message = x.Message,
                Link = x.Link,
                PayloadJson = x.PayloadJson,
                CreatedDate = x.CreatedDate,
                CompanyId = x.CompanyId,
                IsRead = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadDate = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.ReadDate)
                    .FirstOrDefault(),
                CreatedBy = x.CreatedBy,
                CreatedByNameSnapshot = x.CreatedByNameSnapshot
            });

        if (category == NotificationCategory.LegacyData)
        {
            query = query.Where(x =>
                x.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate);
        }
        else if (category != NotificationCategory.All)
        {
            var topics = NotificationTopicCategoryRules.GetTopics(category);
            query = query.Where(x =>
                x.CreatedDate >= NotificationTopicCategoryRules.CurrentDataStartDate &&
                topics.Contains(x.Topic));
        }

        if (afterCreated.HasValue && afterId.HasValue)
        {
            // Keyset paging: lấy các dòng cũ hơn item cuối FE đang có.
            query = query.Where(x =>
                x.CreatedDate < afterCreated.Value ||
                (x.CreatedDate == afterCreated.Value && x.Id.CompareTo(afterId.Value) < 0));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.Id)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.Category = NotificationTopicCategoryRules.GetCategory(item.Topic, item.CreatedDate);
        }

        return items;
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        return await _dbContext.NotificationUserStates
            .AsNoTracking()
            .CountAsync(x =>
                x.UserId == employeeId &&
                !x.IsRead &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId,
                cancellationToken);
    }

    public async Task<NotificationUnreadSummaryDto> GetUnreadSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        var topicCounts = await _dbContext.NotificationUserStates
            .AsNoTracking()
            .Where(x =>
                x.UserId == employeeId &&
                !x.IsRead &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId)
            .GroupBy(x => new
            {
                x.Notification.Topic,
                IsLegacyData = x.Notification.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate
            })
            .Select(group => new
            {
                group.Key.Topic,
                group.Key.IsLegacyData,
                UnreadCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        var categoryCounts = Enum.GetValues<NotificationCategory>()
            .Where(category => category != NotificationCategory.All)
            .ToDictionary(category => category, _ => 0);

        foreach (var topicCount in topicCounts)
        {
            var category = topicCount.IsLegacyData
                ? NotificationCategory.LegacyData
                : NotificationTopicCategoryRules.GetCategory(topicCount.Topic);
            categoryCounts[category] += topicCount.UnreadCount;
        }

        return new NotificationUnreadSummaryDto
        {
            TotalUnread = topicCounts.Sum(x => x.UnreadCount),
            Categories = categoryCounts
                .OrderBy(x => x.Key)
                .Select(x => new NotificationCategoryUnreadCountDto
                {
                    Category = x.Key,
                    UnreadCount = x.Value
                })
                .ToList()
        };
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        var result = await _dbContext.Notifications
            .AsNoTracking()
            .Where(x =>
                x.Id == id &&
                x.CompanyId == companyId &&
                x.UserStates.Any(state => state.UserId == employeeId && !state.IsArchived))
            .Select(x => new NotificationDto
            {
                Id = x.Id,
                Topic = x.Topic,
                Severity = x.Severity,
                Title = x.Title,
                Message = x.Message,
                Link = x.Link,
                PayloadJson = x.PayloadJson,
                CreatedDate = x.CreatedDate,
                CompanyId = x.CompanyId,
                IsRead = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadDate = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.ReadDate)
                    .FirstOrDefault(),
                CreatedBy = x.CreatedBy,
                CreatedByNameSnapshot = x.CreatedByNameSnapshot
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is not null)
        {
            result.Category = NotificationTopicCategoryRules.GetCategory(result.Topic, result.CreatedDate);
        }

        return result;
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        await _dbContext.NotificationUserStates
            .Where(x =>
                x.NotificationId == id &&
                x.UserId == employeeId &&
                !x.IsRead &&
                x.Notification.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadDate, DateTime.Now),
                cancellationToken);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        return await _dbContext.NotificationUserStates
            .Where(x =>
                x.UserId == employeeId &&
                !x.IsRead &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadDate, DateTime.Now),
                cancellationToken);
    }

    public async Task<bool> ArchiveCurrentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();
        var updated = await _dbContext.NotificationUserStates
            .Where(x =>
                x.NotificationId == id &&
                x.UserId == employeeId &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsArchived, true), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> ArchiveRecipientAsync(
        Guid id,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || employeeId == Guid.Empty)
        {
            return false;
        }

        var companyId = GetCurrentCompanyId();
        var actorId = _currentUser.EmployeeId ?? _currentUser.UserId;

        if (actorId == Guid.Empty)
        {
            return false;
        }

        // V1 chi cho nguoi tao notification thu hoi khoi inbox nguoi nhan nham.
        // IsArchived giu lai audit thay vi xoa cung NotificationUserState.
        var updated = await _dbContext.NotificationUserStates
            .Where(x =>
                x.NotificationId == id &&
                x.UserId == employeeId &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId &&
                x.Notification.CreatedBy == actorId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsArchived, true),
                cancellationToken);

        return updated > 0;
    }

    private async Task<HashSet<Guid>> ResolveEmployeeIdsAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> targetUserIds,
        IReadOnlyCollection<string> targetRoles,
        CancellationToken cancellationToken)
    {
        var resolvedEmployeeIds = new HashSet<Guid>();

        if (targetUserIds.Count > 0)
        {
            var directEmployeeIds = await _dbContext.Employees
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    targetUserIds.Contains(x.EmployeeId))
                .Select(x => x.EmployeeId)
                .ToListAsync(cancellationToken);

            foreach (var employeeId in directEmployeeIds)
            {
                resolvedEmployeeIds.Add(employeeId);
            }
        }

        if (targetRoles.Count > 0)
        {
            // Role trong Identity được lưu dạng normalized; chỉ user-role còn active mới được nhận.
            var roleEmployeeIds = await (
                    from role in _dbContext.Roles.AsNoTracking()
                    join userRole in _dbContext.UserRoles.AsNoTracking()
                        on role.Id equals userRole.RoleId
                    join user in _dbContext.Users.AsNoTracking()
                        on userRole.UserId equals user.Id
                    join employee in _dbContext.Employees.AsNoTracking()
                        on user.EmployeeId equals employee.EmployeeId
                    where role.NormalizedName != null &&
                          targetRoles.Contains(role.NormalizedName) &&
                          userRole.IsActive &&
                          user.EmployeeId.HasValue &&
                          employee.CompanyId == companyId &&
                          employee.IsActive
                    select employee.EmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var employeeId in roleEmployeeIds)
            {
                resolvedEmployeeIds.Add(employeeId);
            }
        }

        return resolvedEmployeeIds;
    }

    private Guid GetCurrentCompanyId()
    {
        return _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user does not have a company.");
    }

    private Guid GetCurrentEmployeeId()
    {
        return _currentUser.EmployeeId
            ?? throw new UnauthorizedAccessException("Current user does not have an employee id.");
    }

    private static IReadOnlyCollection<Guid> NormalizeIds(IReadOnlyCollection<Guid>? ids)
    {
        return ids?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();
    }

    private static IReadOnlyCollection<string> NormalizeRoles(IReadOnlyCollection<string>? roles)
    {
        return roles?
            .SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
    }
}
