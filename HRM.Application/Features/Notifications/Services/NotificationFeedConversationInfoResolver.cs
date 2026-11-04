using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

/// <summary>Enriches only the selected feed page, independently of inbox conversation pagination.</summary>
internal sealed class NotificationFeedConversationInfoResolver(
    IInternalMailDbContext db,
    InternalMailAreaAccessService areas,
    InternalConversationSampleRequestInfoResolver sampleRequests,
    InternalConversationQuotationInfoResolver quotations)
{
    public async Task<FeedConversationInfo> ResolveAsync(
        IEnumerable<Guid> conversationIds, Guid companyId, CancellationToken ct)
    {
        var ids = conversationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0 || companyId == Guid.Empty)
            return new(new Dictionary<Guid, SampleRequestConversationInfoDto>(), new Dictionary<Guid, QuotationConversationInfoDto>());

        // Không dùng ID hồ sơ từ payload để vượt scope hội thoại; nhóm riêng phải qua cùng quyền đọc như detail.
        var links = await (from g in areas.VisibleGroupScopes().AsNoTracking()
            where ids.Contains(g.ConversationId)
            join parent in db.InternalConversations.Where(p => p.CompanyId == companyId && p.IsActive)
                on g.RootConversationId equals parent.InternalConversationId
            select new
            {
                Id = g.ConversationId,
                Type = parent.RelatedType,
                RelatedId = parent.RelatedId
            })
            .ToListAsync(ct);
        var samples = await sampleRequests.ResolveAsync(links.Where(link => link.Type == InternalMailRelatedType.SampleRequest)
            .Select(link => link.RelatedId ?? Guid.Empty), companyId, ct);
        var quotes = await quotations.ResolveAsync(links.Where(link => link.Type == InternalMailRelatedType.Quotation)
            .Select(link => link.RelatedId ?? Guid.Empty), companyId, ct);
        return new(
            links.Where(link => link.Type == InternalMailRelatedType.SampleRequest && link.RelatedId.HasValue && samples.ContainsKey(link.RelatedId.Value))
                .ToDictionary(link => link.Id, link => samples[link.RelatedId!.Value]),
            links.Where(link => link.Type == InternalMailRelatedType.Quotation && link.RelatedId.HasValue && quotes.ContainsKey(link.RelatedId.Value))
                .ToDictionary(link => link.Id, link => quotes[link.RelatedId!.Value]));
    }
}

internal sealed record FeedConversationInfo(
    IReadOnlyDictionary<Guid, SampleRequestConversationInfoDto> SampleRequests,
    IReadOnlyDictionary<Guid, QuotationConversationInfoDto> Quotations);
