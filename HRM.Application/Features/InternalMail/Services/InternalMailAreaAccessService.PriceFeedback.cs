using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.NotificationHub.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal const string PriceFeedbackSummary = "Đã ghi nhận phản hồi giá của khách";
    // Optional dependency preserves legacy manual construction; missing pricing decisions always deny content.
    internal bool CanReadPriceFeedback => user.IsAuthenticated &&
        pricingVisibility is not null && SampleRequestPriceFeedbackVisibility.CanViewContent(pricingVisibility.GetAccess());

    internal async Task<Guid[]> PricingCapableEmployeesAsync(Guid company, IEnumerable<Guid> candidates, CancellationToken ct)
    {
        var ids = candidates.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var roles = await (from employee in db.Employees
            join account in db.Users on employee.EmployeeId equals account.EmployeeId
            join membership in db.UserRoles on account.Id equals membership.UserId
            join role in db.Roles on membership.RoleId equals role.Id
            where employee.CompanyId == company && employee.IsActive && ids.Contains(employee.EmployeeId) && membership.IsActive
            select new { employee.EmployeeId, role.Id, role.Name }).ToArrayAsync(ct);
        var roleIds = roles.Select(r => r.Id).Distinct().ToArray();
        var claims = await db.RoleClaims.AsNoTracking().Where(c => roleIds.Contains(c.RoleId) &&
            (c.ClaimType == ApplicationPermissionClaimTypes.Permission || c.ClaimType == ApplicationPermissionClaimTypes.PermissionModelVersion))
            .Select(c => new { c.RoleId, c.ClaimType, c.ClaimValue }).ToArrayAsync(ct);
        // Same per-role resolver used when IdentityAuthenticationService issues runtime permission snapshots.
        return roles.Where(r => r.Name != null && ApplicationPermissionCatalog.ResolveRole(r.Name,
                claims.Any(c => c.RoleId == r.Id && c.ClaimType == ApplicationPermissionClaimTypes.PermissionModelVersion &&
                    c.ClaimValue == ApplicationPermissionClaimTypes.CurrentModelVersion),
                claims.Where(c => c.RoleId == r.Id && c.ClaimType == ApplicationPermissionClaimTypes.Permission)
                    .Select(c => c.ClaimValue ?? ""))
            .Contains(ApplicationPermissions.Pricing.ViewApprovedSellingPrice, StringComparer.OrdinalIgnoreCase))
            .Select(r => r.EmployeeId).Distinct().ToArray();
    }

    internal IQueryable<Guid> RestrictedPriceMessageIds => db.SampleRequestWorkflowEvents
        .Where(e => e.CompanyId == user.CompanyId && e.MessageId != null &&
            SampleRequestPriceFeedbackVisibility.PriceEventTypes.Contains(e.EventType))
        .Select(e => e.MessageId!.Value);

    internal IQueryable<InternalMessage> SearchableMessageBodies(IQueryable<InternalMessage> query)
        => CanReadPriceFeedback ? query : query.Where(m => !RestrictedPriceMessageIds.Contains(m.InternalMessageId));

    private async Task<HashSet<Guid>> RestrictedIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var page = ids.Distinct().ToArray();
        if (CanReadPriceFeedback || page.Length == 0) return [];
        return (await RestrictedPriceMessageIds.Where(id => page.Contains(id)).ToArrayAsync(ct)).ToHashSet();
    }

    internal async Task MaskPriceMessagesAsync(IReadOnlyCollection<InternalMessageDto> messages, CancellationToken ct)
    {
        var ids = messages.Select(m => m.MessageId).Concat(messages.Where(m => m.ReplyTo != null).Select(m => m.ReplyTo!.MessageId));
        var restricted = await RestrictedIdsAsync(ids, ct);
        foreach (var message in messages)
        {
            if (restricted.Contains(message.MessageId))
            {
                message.Body = PriceFeedbackSummary;
                message.PayloadJson = null;
                foreach (var reference in message.References)
                {
                    reference.SnapshotJson = null;
                    reference.RelatedNameSnapshot = null;
                }
            }
            if (message.ReplyTo is { } reply && restricted.Contains(reply.MessageId))
                reply.BodyPreview = PriceFeedbackSummary;
        }
    }

    internal async Task MaskPriceConversationPreviewsAsync(IReadOnlyCollection<InternalConversationListItemDto> items, CancellationToken ct)
    {
        var restricted = await RestrictedIdsAsync(items.Select(m => m.LastMessageId).OfType<Guid>(), ct);
        foreach (var item in items)
            if (item.LastMessageId is { } id && restricted.Contains(id)) item.LastMessageBody = PriceFeedbackSummary;
    }

    internal async Task MaskPriceHubPreviewsAsync(IReadOnlyDictionary<Guid, NotificationHubConversationInfoDto> items, CancellationToken ct)
    {
        if (CanReadPriceFeedback || items.Count == 0) return;
        var ids = items.Keys.ToArray();
        var restricted = await db.InternalConversations.AsNoTracking()
            .Where(c => c.CompanyId == user.CompanyId && ids.Contains(c.InternalConversationId) &&
                RestrictedPriceMessageIds.Contains(c.Messages.Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId)
                    .Select(m => m.InternalMessageId).FirstOrDefault()))
            .Select(c => c.InternalConversationId).ToArrayAsync(ct);
        foreach (var item in items)
            if (restricted.Contains(item.Key)) item.Value.LastMessageBody = PriceFeedbackSummary;
    }

    internal async Task MaskPriceNotificationsAsync(IReadOnlyCollection<NotificationDto> items, CancellationToken ct)
    {
        if (CanReadPriceFeedback || items.Count == 0) return;
        var ids = items.Select(n => n.Id).ToArray();
        var messageIds = items.Select(n => n.MessageId).OfType<Guid>().ToArray();
        var links = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == user.CompanyId &&
                SampleRequestPriceFeedbackVisibility.PriceEventTypes.Contains(e.EventType) &&
                (e.NotificationId != null && ids.Contains(e.NotificationId.Value) ||
                 e.MessageId != null && messageIds.Contains(e.MessageId.Value)))
            .Select(e => new { e.NotificationId, e.MessageId }).ToArrayAsync(ct);
        foreach (var item in items)
            if (links.Any(e => e.NotificationId == item.Id || e.MessageId != null && e.MessageId == item.MessageId))
            {
                item.Title = PriceFeedbackSummary;
                item.Message = PriceFeedbackSummary;
                item.PayloadJson = null;
            }
    }
}
