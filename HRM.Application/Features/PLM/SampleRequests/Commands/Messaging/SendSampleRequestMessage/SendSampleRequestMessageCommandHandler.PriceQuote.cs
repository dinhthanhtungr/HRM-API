using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

internal sealed partial class SendSampleRequestMessageCommandHandler
{
    private async Task<OperationResult<SendInternalMessageResultDto>> PublishPrivatePriceQuoteRequestAsync(
        InternalConversation root, SendSampleRequestMessageCommand request, Guid actor, string body, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestPriceQuoteRequesters) ||
            !await _areas.Recipients(root.CompanyId, InternalMailAreas.General, root.InternalConversationId)
                .ContainsAsync(actor, ct))
            return OperationResult<SendInternalMessageResultDto>.Fail("Price quote request is outside your communication scope.");
        // No public AreaCode/recipient value can widen Pricing membership. Lab may request a price,
        // but this action does not make the sender a member of the private pricing group.
        var (group, eligible) = await GetPricingPublicationGroupAsync(root, ct);
        var now = DateTime.Now;
        await _dbContext.SaveChangesAsync(ct);
        var message = new InternalMessage
        {
            InternalMessageId = Guid.CreateVersion7(), InternalConversationId = group.InternalConversationId,
            SenderEmployeeId = actor, MessageType = InternalMessageType.Action, Body = body,
            SentAt = now, IsUrgent = request.IsUrgent
        };
        message.PayloadJson = JsonSerializer.Serialize(new SampleRequestThreadMessagePayload
        {
            ContentType = ResolveContentType(request), ConversationId = group.InternalConversationId,
            MessageId = message.InternalMessageId, SampleRequestId = request.SampleRequestId,
            ExternalId = root.RelatedExternalId, Type = request.Type.ToString(), IsUrgent = request.IsUrgent,
            PriceQuoteRequest = request.PriceQuoteRequest
        }, PayloadJsonOptions);
        _dbContext.InternalMessages.Add(message);
        group.LastMessageId = message.InternalMessageId;
        group.LastMessageAt = now;
        await AddReadStatesAsync(message.InternalMessageId, actor, eligible, now, ct);
        await UpdateParticipantStatesAsync(group.InternalConversationId, actor, now, ct);
        await _dbContext.SaveChangesAsync(ct);

        var targets = request.NotificationRecipientEmployeeIdsOverride ??
            await _notificationAudience.ResolveAsync(root.CompanyId, request.SampleRequestId,
                TopicNotifications.SampleRequestPriceQuoteRequested, ct);
        var recipients = SampleRequestNotificationRecipientRules.Resolve(actor,
            group.Participants.Where(p => p.IsActive && eligible.Contains(p.EmployeeId))
                .Select(p => new SampleRequestConversationParticipant(p.EmployeeId, p.IsMuted)).ToArray(),
            targets.Where(eligible.Contains).ToArray());
        var notification = await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            UseExactAudience = true, CompanyId = root.CompanyId, CreatedBy = actor,
            Topic = TopicNotifications.SampleRequestPriceQuoteRequested,
            Title = request.TitleOverride ?? BuildTitle(request.Type), Message = body,
            Severity = request.IsUrgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
            AggregateId = request.SampleRequestId, AggregateCode = root.RelatedExternalId,
            ConversationId = group.InternalConversationId, MessageId = message.InternalMessageId,
            PayloadJson = message.PayloadJson,
            Link = request.LinkOverride ?? $"/plm/sample-requests/{request.SampleRequestId}",
            TargetUserIds = recipients.TargetEmployeeIds, SilentUserIds = recipients.SilentEmployeeIds
        }, ct);
        return OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto
        { ConversationId = group.InternalConversationId, MessageId = message.InternalMessageId, NotificationId = notification });
    }
}
