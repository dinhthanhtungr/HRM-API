using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
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
