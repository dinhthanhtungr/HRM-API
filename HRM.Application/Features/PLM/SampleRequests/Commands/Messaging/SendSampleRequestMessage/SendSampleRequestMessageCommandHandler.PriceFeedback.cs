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
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

internal sealed partial class SendSampleRequestMessageCommandHandler
{
    private async Task<OperationResult<SendInternalMessageResultDto>> PublishPrivatePriceFeedbackAsync(
        InternalConversation root, SampleRequestWorkflowEvent evt, string body, string title, CancellationToken ct)
    {
        var actor = evt.RecordedByEmployeeId!.Value;
        var standardApproval = evt.EventType == SampleFlowRules.StandardPriceApproved;
        var now = DateTime.Now;
        var (group, recipients) = await GetPricingPublicationGroupAsync(root, ct);
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
                    formulaId = evt.FormulaId, conversationId = group.InternalConversationId,
                    sampleRequestExternalId = root.RelatedExternalId, productPricingVersionId = evt.ProductPricingVersionId,
                    type = standardApproval ? "SampleStandardPriceApproved" : "PriceFeedback" })
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
            Topic = standardApproval ? TopicNotifications.SampleRequestReferencePriceAvailable : TopicNotifications.SampleRequestCustomerPriceFeedbackRecorded,
            Title = title, Message = body,
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
