using System.Text.Json;
using System.Text.Json.Nodes;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Models;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed record InternalMailMention(Guid EmployeeId, string DisplayName);

/// <summary>Stores mention snapshots without replacing workflow payloads or granting membership.</summary>
public static class InternalMailMentions
{
    public const string AudienceMarker = "{\"notificationAudienceVersion\":1}";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Write(string? payload, IReadOnlyCollection<InternalMailMention> mentions,
        IEnumerable<Guid>? previouslyNotified = null, bool markAudience = true)
    {
        var root = string.IsNullOrWhiteSpace(payload) ? new JsonObject() : JsonNode.Parse(payload)!.AsObject();
        if (markAudience) root["notificationAudienceVersion"] = 1;
        root["mentions"] = JsonSerializer.SerializeToNode(mentions, JsonOptions);
        root["notifiedMentionEmployeeIds"] = JsonSerializer.SerializeToNode(
            (previouslyNotified ?? []).Concat(mentions.Select(m => m.EmployeeId)).Distinct().ToArray(), JsonOptions);
        return root.ToJsonString(JsonOptions);
    }

    public static IReadOnlyList<InternalMailMention> Read(string? payload)
        => ReadProperty<InternalMailMention>(payload, "mentions");

    public static IReadOnlyList<Guid> Notified(string? payload)
        => ReadProperty<Guid>(payload, "notifiedMentionEmployeeIds");

    public static IReadOnlyList<Guid> DeliveryMentions(string? payload)
        => ReadProperty<Guid>(payload, "deliveryMentionEmployeeIds");

    private static IReadOnlyList<T> ReadProperty<T>(string? payload, string property)
    {
        if (string.IsNullOrWhiteSpace(payload)) return [];
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [];
            return doc.RootElement.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
                ? values.Deserialize<T[]>(JsonOptions) ?? [] : [];
        }
        catch (JsonException) { return []; }
    }
}

public sealed class InternalMailMentionResolver(IInternalMailDbContext db, InternalMailAreaAccessService areas)
{
    public async Task<OperationResult<IReadOnlyList<InternalMailMention>>> ResolveAsync(
        Guid companyId, Guid conversationId, IReadOnlyCollection<Guid>? requested, CancellationToken ct)
    {
        var ids = requested?.Distinct().ToArray() ?? [];
        if (ids.Length > 50 || ids.Contains(Guid.Empty))
            return OperationResult<IReadOnlyList<InternalMailMention>>.Fail("Mention at most 50 valid participants.");
        if (ids.Length == 0) return OperationResult<IReadOnlyList<InternalMailMention>>.Ok([]);
        var eligible = areas.Recipients(companyId, null, conversationId);
        if (!areas.CanObservePricing && await db.InternalConversations.AnyAsync(c => c.CompanyId == companyId &&
                c.InternalConversationId == conversationId && c.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationPricing, ct))
        {
            var hidden = areas.RoleEmployees(companyId, HRM.Application.Commons.Authorization.ApplicationRoleSets.InternalMail.PricingObservers);
            eligible = eligible.Where(id => !hidden.Contains(id));
        }
        var mentions = await db.InternalConversationParticipants.AsNoTracking()
            .Where(p => p.InternalConversationId == conversationId && p.IsActive && ids.Contains(p.EmployeeId) &&
                p.Conversation.CompanyId == companyId && p.Conversation.IsActive && eligible.Contains(p.EmployeeId))
            .Join(db.Employees, p => p.EmployeeId, e => e.EmployeeId,
                (p, e) => new InternalMailMention(e.EmployeeId, e.FullName))
            .ToArrayAsync(ct);
        return mentions.Length == ids.Length
            ? OperationResult<IReadOnlyList<InternalMailMention>>.Ok(mentions)
            : OperationResult<IReadOnlyList<InternalMailMention>>.Fail("Mentioned employees must be active participants of this area.");
    }
}
