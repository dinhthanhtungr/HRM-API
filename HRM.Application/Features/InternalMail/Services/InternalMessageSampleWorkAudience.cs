using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Decorates an already authorized message page with a business-side navigation hint.</summary>
internal static class InternalMessageSampleWorkAudience
{
    internal static async Task AttachAsync(IReadOnlyCollection<InternalMessageDto> messages,
        IInternalMailDbContext db, InternalMailAreaAccessService areas, Guid company, CancellationToken ct)
    {
        foreach (var message in messages)
        {
            message.SenderSampleWorkAudience = null;
            message.SampleWorkTrackingEligible = null;
        }
        var eligible = messages.Where(x => !x.IsDeleted).ToArray();
        if (company == Guid.Empty || eligible.Length == 0) return;
        var conversationIds = eligible.Select(x => x.ConversationId).Distinct().ToArray();
        var contexts = await (from groupScope in areas.GroupScopes(company)
                              join root in db.InternalConversations.AsNoTracking()
                                  on groupScope.RootConversationId equals root.InternalConversationId
                              join request in db.SampleRequests.AsNoTracking() on root.RelatedId equals (Guid?)request.SampleRequestId
                              where conversationIds.Contains(groupScope.ConversationId) && root.CompanyId == company &&
                                  root.IsActive && root.RelatedType == InternalMailRelatedType.SampleRequest && root.RelatedId.HasValue &&
                                  request.CompanyId == company && request.IsActive
                              select new { groupScope.ConversationId, RequestId = request.SampleRequestId,
                                  TrackingEligible = request.CreatedDate >= SampleFlowRules.StartsAt })
            .ToDictionaryAsync(x => x.ConversationId, ct);
        eligible = eligible.Where(x => contexts.ContainsKey(x.ConversationId)).ToArray();
        if (eligible.Length == 0) return;
        foreach (var message in eligible) message.SampleWorkTrackingEligible = contexts[message.ConversationId].TrackingEligible;
        var byMessage = eligible.ToDictionary(x => x.MessageId);
        var ids = byMessage.Keys.ToArray();
        var requestIds = contexts.Values.Select(x => x.RequestId).Distinct().ToArray();
        var events = await db.SampleRequestWorkflowEvents.AsNoTracking()
            .Where(x => x.CompanyId == company && x.MessageId.HasValue && ids.Contains(x.MessageId.Value) && requestIds.Contains(x.SampleRequestId))
            .Select(x => new { x.MessageId, x.ConversationId, x.SampleRequestId, x.RecordedByEmployeeId, x.EventType })
            .ToArrayAsync(ct);
        var ambiguous = new HashSet<Guid>();
        foreach (var group in events.Where(x => byMessage.TryGetValue(x.MessageId!.Value, out var message) &&
                     x.ConversationId == message.ConversationId && x.SampleRequestId == contexts[message.ConversationId].RequestId &&
                     x.RecordedByEmployeeId == message.SenderEmployeeId).GroupBy(x => x.MessageId!.Value))
        {
            var sides = group.Select(x => FromEvent(x.EventType)).Where(x => x is not null).Distinct().ToArray();
            if (sides.Length == 1) byMessage[group.Key].SenderSampleWorkAudience = sides[0];
            else if (sides.Length > 1) ambiguous.Add(group.Key);
        }
        var senderIds = eligible.Where(x => x.SenderSampleWorkAudience is null && !ambiguous.Contains(x.MessageId))
            .Select(x => x.SenderEmployeeId).Distinct().ToArray();
        if (senderIds.Length == 0) return;
        var memberships = await (from employee in db.Employees.AsNoTracking()
                                 join account in db.Users.AsNoTracking() on employee.EmployeeId equals account.EmployeeId
                                 join membership in db.UserRoles.AsNoTracking() on account.Id equals membership.UserId
                                 join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                                 where employee.CompanyId == company && employee.IsActive && senderIds.Contains(employee.EmployeeId) && membership.IsActive
                                 select new { employee.EmployeeId, role.Name, role.NormalizedName }).ToArrayAsync(ct);
        var bySender = memberships.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key,
            x => FromRoles(x.SelectMany(r => new[] { r.Name, r.NormalizedName })));
        foreach (var message in eligible.Where(x => x.SenderSampleWorkAudience is null && !ambiguous.Contains(x.MessageId)))
            message.SenderSampleWorkAudience = bySender.GetValueOrDefault(message.SenderEmployeeId);
    }

    internal static string? FromRoles(IEnumerable<string?> roles)
    {
        var names = roles.Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sale = names.Contains(ApplicationRoles.Sales.SaleUser) || names.Contains(ApplicationRoles.Sales.SaleAdmin);
        var lab = names.Contains(ApplicationRoles.Lab.LabUser) || names.Contains(ApplicationRoles.Lab.LabAdmin);
        return sale == lab ? null : sale ? "Sale" : "Lab";
    }

    internal static string? FromEvent(string type) => type switch
    {
        SampleRequestWorkflowEvents.LabNote or SampleRequestWorkflowEvents.FormulaConfirmed or
            SampleRequestWorkflowEvents.StandardReceived or SampleRequestWorkflowEvents.SampleSent or
            SampleFlowRules.QuotationFinalized or SampleFlowRules.ChipFinalized or SampleFlowRules.ChipReopened => "Lab",
        SampleRequestWorkflowEvents.Created or SampleRequestWorkflowEvents.PriceAccepted or SampleRequestWorkflowEvents.PriceRevoked or
            SampleRequestWorkflowEvents.FeedbackRecorded or SampleFlowRules.KgRequested or SampleFlowRules.ChipRequested or
            SampleFlowRules.LabFinalizationRequested or SampleFlowRules.CompletedWithoutTrial or SampleFlowRules.PriceFeedbackShared or
            SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse or SampleFlowRules.LowerPriceRequested or
            SampleFlowRules.PriceFollowUpDeferred or "CustomerPriceConfirmed" or "CustomerPriceConfirmationRevoked" => "Sale",
        _ => null
    };
}
