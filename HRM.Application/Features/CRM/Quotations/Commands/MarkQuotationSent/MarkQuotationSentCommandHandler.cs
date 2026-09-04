using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HRM.Application.Features.CRM.Quotations.Commands.MarkQuotationSent
{
    internal sealed class MarkQuotationSentCommandHandler
        : IRequestHandler<MarkQuotationSentCommand, OperationResult>
    {
        private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly string[] GlobalManagementRoles =
        [
            ApplicationRoles.President.ToUpperInvariant(),
            ApplicationRoles.Developer.ToUpperInvariant(),
        ];

        private readonly ICRMReadDbContext _readDbContext;
        private readonly ICRMWriteDbContext _writeDbContext;
        private readonly IInternalMailDbContext _internalMailDbContext;
        private readonly ICustomerVisibilityService _visibilityService;
        private readonly ISaleGroupRecipientResolver _saleGroupRecipientResolver;
        private readonly INotificationService _notificationService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly KeyedMutationLock<Guid> _mutationLock;

        public MarkQuotationSentCommandHandler(
            ICRMReadDbContext readDbContext,
            ICRMWriteDbContext writeDbContext,
            IInternalMailDbContext internalMailDbContext,
            ICustomerVisibilityService visibilityService,
            ISaleGroupRecipientResolver saleGroupRecipientResolver,
            INotificationService notificationService,
            IDateTimeProvider dateTimeProvider,
            KeyedMutationLock<Guid> mutationLock)
        {
            _readDbContext = readDbContext;
            _writeDbContext = writeDbContext;
            _internalMailDbContext = internalMailDbContext;
            _visibilityService = visibilityService;
            _saleGroupRecipientResolver = saleGroupRecipientResolver;
            _notificationService = notificationService;
            _dateTimeProvider = dateTimeProvider;
            _mutationLock = mutationLock;
        }

        public async Task<OperationResult> Handle(
            MarkQuotationSentCommand command,
            CancellationToken cancellationToken)
        {
            if (command.QuotationId == Guid.Empty)
            {
                return OperationResult.Fail("QuotationId is invalid.");
            }

            if (!IsSupportedInteractionType(command.Request.InteractionType))
            {
                return OperationResult.Fail("InteractionType must be Quotation.");
            }

            using var mutationLease = await _mutationLock.AcquireAsync(
                command.QuotationId,
                cancellationToken);

            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await _writeDbContext.Quotations
                .AsTracking()
                .Include(x => x.Customer)
                .Include(x => x.Lines)
                    .ThenInclude(x => x.PriceTiers)
                .FirstOrDefaultAsync(
                    x =>
                        x.QuotationId == command.QuotationId &&
                        x.CompanyId == scope.CompanyId &&
                        x.IsActive,
                    cancellationToken);

            if (quotation is null)
            {
                return OperationResult.Fail("Quotation was not found or is outside your visibility scope.");
            }
            var canAccessCustomer = await _visibilityService
                .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken);
            if (!canAccessCustomer)
            {
                return OperationResult.Fail(
                    "Quotation was not found or is outside your visibility scope.");
            }

            var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
                command.Request.ExpectedUpdatedDate,
                quotation.UpdatedDate,
                "Quotation");
            if (concurrencyError is not null)
            {
                return OperationResult.Fail(concurrencyError);
            }

            if (quotation.Status == QuotationStatus.Sent)
            {
                return OperationResult.Ok("Quotation was already marked as sent.");
            }

            if (quotation.SaleEmployeeId != scope.EmployeeId)
            {
                return OperationResult.Fail(
                    "Only the assigned sale employee can mark this quotation as sent.");
            }

            if (!QuotationWorkflowRules.CanMarkSent(quotation.Status))
            {
                return OperationResult.Fail(
                    "This quotation status cannot be marked as sent.");
            }

            var previousStatus = quotation.Status;

            var activeLines = quotation.Lines.Where(line => line.IsActive).ToArray();
            if (activeLines.Length == 0)
            {
                return OperationResult.Fail("A quotation must have at least one line before it can be sent.");
            }

            if (activeLines.Any(line =>
                    !line.PriceTiers.Any(tier => tier.IsActive) ||
                    line.PriceTiers.Any(tier =>
                        tier.IsActive &&
                        (tier.UnitPrice < 0m || tier.CommissionAmount < 0m))))
            {
                return OperationResult.Fail(
                    "Every quotation line must have a complete non-negative tiered snapshot " +
                    "before it can be sent.");
            }

            var recipientEmployeeIds = await ResolveManagementRecipientsAsync(
                quotation.CompanyId,
                quotation.SaleEmployeeId,
                scope.EmployeeId,
                cancellationToken);
            if (recipientEmployeeIds.Count == 0)
            {
                return OperationResult.Fail(
                    "No active sale group leader, President or Developer employee was found in the current company.");
            }

            var now = _dateTimeProvider.Now;
            var actorName = await _internalMailDbContext.Employees
                .AsNoTracking()
                .Where(x => x.EmployeeId == scope.EmployeeId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            actorName = QuotationRules.TrimToNull(actorName) ?? "Nhân viên kinh doanh";

            var conversation = await FindOrCreateConversationAsync(
                quotation,
                scope.EmployeeId,
                now,
                cancellationToken);

            var participantIds = recipientEmployeeIds
                .Append(scope.EmployeeId)
                .Distinct()
                .ToArray();
            await EnsureParticipantsAsync(
                conversation.InternalConversationId,
                scope.EmployeeId,
                participantIds,
                now,
                cancellationToken);

            quotation.Status = QuotationStatus.Sent;
            quotation.SentDate = now;
            quotation.UpdatedBy = scope.EmployeeId;
            quotation.UpdatedDate = now;

            var interactionContent = QuotationSentContentBuilder.Build(quotation);
            var interaction = new CustomerInteraction
            {
                Id = Guid.CreateVersion7(),
                CustomerId = quotation.CustomerId,
                ContactId = quotation.ContactId,
                InteractionType = command.Request.InteractionType,
                Subject = $"Đã gửi báo giá {quotation.ExternalId}",
                Content = interactionContent,
                Outcome = "Đã gửi báo giá",
                NextAction = "Theo dõi phản hồi của khách hàng",
                InteractionAt = now,
                AssignedSaleEmployeeId = quotation.SaleEmployeeId,
                CompanyId = quotation.CompanyId,
                CreatedDate = now,
                CreatedBy = scope.EmployeeId,
                IsActive = true
            };

            var message = new InternalMessage
            {
                InternalMessageId = Guid.CreateVersion7(),
                InternalConversationId = conversation.InternalConversationId,
                SenderEmployeeId = scope.EmployeeId,
                MessageType = InternalMessageType.Action,
                Body =
                    $"{actorName} đã gửi báo giá {quotation.ExternalId} cho khách hàng " +
                    $"{quotation.Customer.CustomerName}.\n\n{interactionContent}",
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
                Note = QuotationRules.TrimToNull(command.Request.Note),
                ChangedBy = scope.EmployeeId,
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
                CreatedBy = scope.EmployeeId
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
                        IsRead = employeeId == scope.EmployeeId,
                        ReadAt = employeeId == scope.EmployeeId ? now : null
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
                    CreatedBy = scope.EmployeeId,
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

        private async Task<InternalConversation> FindOrCreateConversationAsync(
            Quotation quotation,
            Guid currentEmployeeId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var subject = QuotationConversationSubjectService.BuildSubject(
                quotation.ExternalId,
                quotation.Lines.Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.QuotationLineId)
                    .Select(x => x.ProductExternalIdSnapshot));
            var conversation = await _internalMailDbContext.InternalConversations
                .AsTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.CompanyId == quotation.CompanyId &&
                        x.IsActive &&
                        x.RelatedType == InternalMailRelatedType.Quotation &&
                        x.RelatedId == quotation.QuotationId,
                    cancellationToken);
            if (conversation is not null)
            {
                if (!string.Equals(conversation.Subject, subject, StringComparison.Ordinal))
                {
                    conversation.Subject = subject;
                }

                return conversation;
            }

            conversation = new InternalConversation
            {
                InternalConversationId = Guid.CreateVersion7(),
                CompanyId = quotation.CompanyId,
                Subject = subject,
                RelatedType = InternalMailRelatedType.Quotation,
                RelatedId = quotation.QuotationId,
                RelatedExternalId = quotation.ExternalId,
                CreatedBy = currentEmployeeId,
                CreatedAt = now,
                LastMessageAt = now,
                IsActive = true
            };

            await _internalMailDbContext.InternalConversations.AddAsync(conversation, cancellationToken);

            // Lưu conversation trước để tránh chu trình FK với LastMessageId khi thêm message đầu tiên.
            await _internalMailDbContext.SaveChangesAsync(cancellationToken);
            return conversation;
        }

        private async Task EnsureParticipantsAsync(
            Guid conversationId,
            Guid senderEmployeeId,
            IReadOnlyCollection<Guid> participantIds,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var existingParticipants = await _internalMailDbContext.InternalConversationParticipants
                .AsTracking()
                .Where(x =>
                    x.InternalConversationId == conversationId &&
                    participantIds.Contains(x.EmployeeId))
                .ToListAsync(cancellationToken);
            var participantByEmployeeId = existingParticipants.ToDictionary(x => x.EmployeeId);

            foreach (var employeeId in participantIds)
            {
                if (participantByEmployeeId.TryGetValue(employeeId, out var participant))
                {
                    participant.IsActive = true;
                    participant.DeletedAt = null;
                    participant.DeletedByEmployeeId = null;
                    participant.IsArchived = false;
                    participant.ArchivedAt = null;
                    participant.IsMuted = false;
                    if (employeeId == senderEmployeeId)
                    {
                        participant.Role = InternalConversationParticipantRole.Owner;
                        participant.LastReadAt = now;
                    }

                    continue;
                }

                await _internalMailDbContext.InternalConversationParticipants.AddAsync(
                    new InternalConversationParticipant
                    {
                        InternalConversationId = conversationId,
                        EmployeeId = employeeId,
                        Role = employeeId == senderEmployeeId
                            ? InternalConversationParticipantRole.Owner
                            : InternalConversationParticipantRole.Member,
                        JoinedAt = now,
                        LastReadAt = employeeId == senderEmployeeId ? now : null,
                        IsArchived = false,
                        IsMuted = false
                    },
                    cancellationToken);
            }
        }

        private async Task<IReadOnlyCollection<Guid>> ResolveManagementRecipientsAsync(
            Guid companyId,
            Guid saleEmployeeId,
            Guid senderEmployeeId,
            CancellationToken cancellationToken)
        {
            var saleGroupLeaderIds = await _saleGroupRecipientResolver.ResolveSaleGroupLeaderIdsAsync(
                companyId,
                saleEmployeeId,
                senderEmployeeId,
                cancellationToken);

            var globalManagementIds = await _saleGroupRecipientResolver.ResolveActiveEmployeeIdsByRolesAsync(
                companyId,
                GlobalManagementRoles,
                senderEmployeeId,
                cancellationToken);

            return saleGroupLeaderIds
                .Concat(globalManagementIds)
                .Distinct()
                .ToArray();
        }

        private static bool IsSupportedInteractionType(CustomerInteractionType interactionType)
            => interactionType == CustomerInteractionType.Quotation;
    }

}
