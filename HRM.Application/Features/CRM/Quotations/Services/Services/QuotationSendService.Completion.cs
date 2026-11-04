using System.Text.Json;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    private async Task<OperationResult> CompleteSentAsync(Quotation quotation, QuotationStatus previousStatus,
        CustomerInteractionType interactionType, string? reason, InternalConversation conversation, Guid[] participantIds,
        IReadOnlyCollection<Guid> recipientEmployeeIds, Guid actorId, string actorName, DateTime now,
        CancellationToken cancellationToken)
    {
        quotation.Status = QuotationStatus.Sent;
        quotation.SentDate = now;
        quotation.UpdatedBy = actorId;
        quotation.UpdatedDate = QuotationWorkflowRules.NextUpdatedDate(now, quotation.UpdatedDate);
        if (previousStatus != QuotationStatus.PendingSendConfirmation)
            await SyncLinkedSampleQuoteDatesAsync(quotation, actorId, now, cancellationToken);

        var interactionContent = QuotationSentContentBuilder.Build(quotation);
        var interaction = new CustomerInteraction
        {
            Id = Guid.CreateVersion7(),
            CustomerId = quotation.CustomerId,
            ContactId = quotation.ContactId,
            InteractionType = interactionType,
            Subject = $"Đã gửi báo giá {quotation.ExternalId}",
            Content = interactionContent,
            Outcome = "Đã gửi báo giá",
            NextAction = "Theo dõi phản hồi của khách hàng",
            InteractionAt = now,
            AssignedSaleEmployeeId = quotation.SaleEmployeeId,
            CompanyId = quotation.CompanyId,
            CreatedDate = now,
            CreatedBy = actorId,
            IsActive = true
        };

        var message = new InternalMessage
        {
            InternalMessageId = Guid.CreateVersion7(),
            InternalConversationId = conversation.InternalConversationId,
            SenderEmployeeId = actorId,
            MessageType = InternalMessageType.Action,
            Body =
                $"{actorName} đã gửi báo giá {quotation.ExternalId} cho khách hàng " +
                $"{quotation.Customer.CustomerName}.\n\n{interactionContent}" +
                (string.IsNullOrWhiteSpace(reason) ? string.Empty : $"\nLý do: {reason.Trim()}"),
            IsUrgent = false,
            SentAt = now,
            IsEdited = false,
            IsDeleted = false
        };

        var payload = new QuotationSentMessagePayload
        {
            ConversationId = conversation.InternalConversationId,
            MessageId = message.InternalMessageId,
            QuotationId = quotation.QuotationId,
            QuotationExternalId = quotation.ExternalId,
            CustomerInteractionId = interaction.Id,
            CustomerId = quotation.CustomerId
        };
        message.PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);

        var history = new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(),
            QuotationId = quotation.QuotationId,
            FromStatus = previousStatus,
            ToStatus = QuotationStatus.Sent,
            Note = null,
            ChangedBy = actorId,
            ChangedDate = now
        };

        quotation.StatusHistories.Add(history);
        _writeDbContext.QuotationStatusHistories.Add(history);
        _writeDbContext.CustomerInteractions.Add(interaction);
        _writeDbContext.CustomerInteractionReferences.Add(new CustomerInteractionReference
        {
            Id = Guid.CreateVersion7(),
            InteractionId = interaction.Id,
            ReferenceType = CustomerInteractionReferenceType.Quotation,
            ReferenceId = quotation.QuotationId,
            ReferenceCodeSnapshot = quotation.ExternalId,
            ReferenceNameSnapshot = quotation.Status.ToString(),
            IsPrimary = true,
            CompanyId = quotation.CompanyId,
            CreatedDate = now,
            CreatedBy = actorId
        });
        await _internalMailDbContext.InternalMessages.AddAsync(message, cancellationToken);

        conversation.LastMessageId = message.InternalMessageId;
        conversation.LastMessageAt = now;

        foreach (var employeeId in participantIds)
        {
            await _internalMailDbContext.InternalMessageReadStates.AddAsync(
                new InternalMessageReadState
                {
                    InternalMessageId = message.InternalMessageId,
                    EmployeeId = employeeId,
                    IsRead = employeeId == actorId,
                    ReadAt = employeeId == actorId ? now : null
                },
                cancellationToken);
        }

        try
        {
            var affectedRows = await _writeDbContext.SaveChangesAsync(cancellationToken);
            if (affectedRows == 0)
            {
                return OperationResult.Fail("Quotation status update was not persisted.");
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult.Fail(
                OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
        }

        await _notificationService.PublishAsync(
            new PublishNotificationRequest
            {
                CompanyId = quotation.CompanyId,
                CreatedBy = actorId,
                CreatedByNameSnapshot = actorName,
                Topic = TopicNotifications.QuotationSent,
                Severity = NotificationSeverity.Info,
                Title = $"Đã gửi báo giá {quotation.ExternalId}",
                Message =
                    $"{actorName} đã gửi báo giá {quotation.ExternalId} cho " +
                    $"{quotation.Customer.CustomerName}.",
                Link = $"/crm/quotations/{quotation.QuotationId}",
                AggregateId = quotation.QuotationId,
                AggregateCode = quotation.ExternalId,
                ConversationId = conversation.InternalConversationId,
                MessageId = message.InternalMessageId,
                PayloadJson = message.PayloadJson,
                TargetUserIds = recipientEmployeeIds
            },
            cancellationToken);

        return OperationResult.Ok("Quotation marked as sent successfully.");
    }
}
