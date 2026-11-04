using System.Text.Json;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

internal sealed partial class SendSampleRequestMessageCommandHandler
{
    private async Task<OperationResult<SendInternalMessageResultDto>> PublishPrivatePriceFeedbackAsync(
        InternalConversation root, SampleRequestWorkflowEvent evt, string body, string title, CancellationToken ct)
    {
        var actor = evt.RecordedByEmployeeId!.Value;
        var now = DateTime.Now;
        var pricingId = await _areas.GroupScopes(root.CompanyId)
            .Where(g => g.RootConversationId == root.InternalConversationId && g.GroupType == InternalConversationGroupType.Pricing)
            .Select(g => (Guid?)g.ConversationId).SingleOrDefaultAsync(ct);
        var group = pricingId is { } id
            ? await _dbContext.InternalConversations.Include(c => c.Participants)
                .SingleAsync(c => c.InternalConversationId == id && c.CompanyId == root.CompanyId && c.IsActive, ct)
            : new InternalConversation
            {
                // GROUP-STORAGE-TRANSITION: legacy-write-shape — shared atomic writer persists group mapping.
                InternalConversationId = Guid.CreateVersion7(), CompanyId = root.CompanyId,
                RelatedType = InternalMailRelatedType.ConversationPricing, RelatedId = root.InternalConversationId,
                RelatedExternalId = root.RelatedExternalId, Subject = root.Subject,
                CreatedBy = root.CreatedBy, CreatedAt = now, LastMessageAt = root.CreatedAt, IsActive = true
            };
        if (pricingId is null) _dbContext.InternalConversations.Add(group);
        var defaults = await _areas.DefaultMembersAsync(root.InternalConversationId, InternalMailAreas.Pricing, ct, root.CompanyId);
        // Existing pricing membership policy intersected with runtime DB-backed selling-price permission.
        var capable = await _areas.PricingCapableEmployeesAsync(root.CompanyId,
            defaults.Concat(group.Participants.Where(p => p.IsActive).Select(p => p.EmployeeId)), ct);
        var existing = group.Participants.Select(p => p.EmployeeId).ToHashSet();
        foreach (var employee in defaults.Where(id => capable.Contains(id) && !existing.Contains(id)))
            group.Participants.Add(new InternalConversationParticipant
            {
                InternalConversationId = group.InternalConversationId, EmployeeId = employee,
                Role = InternalConversationParticipantRole.Member, JoinedAt = now
            });
        var recipients = group.Participants.Where(p => p.IsActive && capable.Contains(p.EmployeeId))
            .Select(p => p.EmployeeId).Distinct().ToArray();
        await _dbContext.SaveChangesAsync(ct);
        var message = await _dbContext.InternalMessages.FirstOrDefaultAsync(m =>
            m.InternalConversationId == group.InternalConversationId && m.PayloadJson != null &&
            EF.Functions.JsonContains(m.PayloadJson, JsonSerializer.Serialize(new { workflowEventId = evt.Id })), ct);
        if (message is null)
        {
            message = new InternalMessage
            {
                InternalMessageId = Guid.CreateVersion7(), InternalConversationId = group.InternalConversationId,
                SenderEmployeeId = actor, MessageType = InternalMessageType.Action, Body = body, SentAt = now,
                PayloadJson = JsonSerializer.Serialize(new { workflowEventId = evt.Id, sampleRequestId = evt.SampleRequestId,
                    formulaId = evt.FormulaId, conversationId = group.InternalConversationId, type = "PriceFeedback" })
            };
            _dbContext.InternalMessages.Add(message);
            group.LastMessageId = message.InternalMessageId;
            group.LastMessageAt = now;
            await AddReadStatesAsync(message.InternalMessageId, actor, recipients, now, ct);
            await UpdateParticipantStatesAsync(group.InternalConversationId, actor, now, ct);
            await _dbContext.SaveChangesAsync(ct);
        }
        var notification = await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            UseExactAudience = true, CompanyId = evt.CompanyId, CreatedBy = actor,
            Topic = TopicNotifications.SampleRequestCustomerPriceFeedbackRecorded, Title = title, Message = body,
            AggregateId = evt.SampleRequestId, AggregateCode = root.RelatedExternalId,
            ConversationId = group.InternalConversationId, MessageId = message.InternalMessageId,
            PayloadJson = message.PayloadJson, Link = $"/plm/sample-requests/{evt.SampleRequestId}",
            TargetUserIds = recipients.Where(id => id != actor).ToArray()
        }, ct);
        return OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto
        {
            ConversationId = group.InternalConversationId, MessageId = message.InternalMessageId, NotificationId = notification
        });
    }
}
