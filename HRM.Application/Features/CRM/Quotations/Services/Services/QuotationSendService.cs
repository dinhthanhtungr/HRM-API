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
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HRM.Application.Features.CRM.Quotations.Services
{
    internal sealed partial class QuotationSendService
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

        public QuotationSendService(
            ICRMReadDbContext readDbContext,
            ICRMWriteDbContext writeDbContext,
            IInternalMailDbContext internalMailDbContext,
            ICustomerVisibilityService visibilityService,
            ISaleGroupRecipientResolver saleGroupRecipientResolver,
            INotificationService notificationService,
            IDateTimeProvider dateTimeProvider,
            KeyedMutationLock<Guid> mutationLock,
            ICurrentUserPermissionService permissions, IOptions<QuotationFeatureOptions> options)
        {
            _readDbContext = readDbContext;
            _writeDbContext = writeDbContext;
            _internalMailDbContext = internalMailDbContext;
            _visibilityService = visibilityService;
            _saleGroupRecipientResolver = saleGroupRecipientResolver;
            _notificationService = notificationService;
            _dateTimeProvider = dateTimeProvider;
            _mutationLock = mutationLock;
            _permissions = permissions;
            _options = options.Value;
        }

        private async Task<OperationResult> ApplyAsync(
            Guid quotationId, MarkQuotationSentRequest request, Guid? requestMessageId,
            CancellationToken cancellationToken, bool confirmationOnly = false)
        {
            if (quotationId == Guid.Empty)
            {
                return OperationResult.Fail("QuotationId is invalid.");
            }

            if (!IsSupportedInteractionType(request.InteractionType))
            {
                return OperationResult.Fail("InteractionType must be Quotation.");
            }



            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await _writeDbContext.Quotations
                .AsTracking()
                .Include(x => x.Customer)
                .Include(x => x.Lines)
                    .ThenInclude(x => x.PriceTiers)
                .Include(x => x.Terms)
                .FirstOrDefaultAsync(
                    x =>
                        x.QuotationId == quotationId &&
                        x.CompanyId == scope.CompanyId &&
                        x.IsActive,
                    cancellationToken);

            if (quotation is null)
            {
                if (confirmationOnly) return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.NotFound,
                    "Quotation was not found or is outside your visibility scope.");
                return OperationResult.Fail("Quotation was not found or is outside your visibility scope.");
            }
            var canAccessCustomer = await _visibilityService
                .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken);
            if (!canAccessCustomer)
            {
                if (confirmationOnly) return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.NotFound,
                    "Quotation was not found or is outside your visibility scope.");
                return OperationResult.Fail(
                    "Quotation was not found or is outside your visibility scope.");
            }

            if (confirmationOnly && !_permissions.HasPermission(ApplicationPermissions.Quotations.ConfirmSent))
                return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.Forbidden, "You cannot confirm quotation sending.");

            var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
                request.ExpectedUpdatedDate,
                quotation.UpdatedDate,
                "Quotation");
            if (concurrencyError is not null)
            {
                if (confirmationOnly) return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.Conflict, concurrencyError);
                return OperationResult.Fail(concurrencyError);
            }

            if (confirmationOnly)
            {
                if (quotation.Status != QuotationStatus.PendingSendConfirmation)
                    return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.NotPending, "Quotation is no longer awaiting send confirmation.");
                var selection = await ResolveConfirmationRequestAsync(quotation, requestMessageId, cancellationToken);
                if (selection.Error is not null) return selection.Error;
                requestMessageId = selection.Message!.InternalMessageId;
            }

            if (requestMessageId.HasValue && !_permissions.HasPermission(ApplicationPermissions.Quotations.ConfirmSent))
                return OperationResult.Fail("You cannot confirm quotation sending.");

            if (quotation.Status == QuotationStatus.Sent)
            {
                if (requestMessageId.HasValue)
                {
                    var completed = await LoadPendingMessageAsync(quotation, requestMessageId.Value, cancellationToken);
                    var completedPayload = ReadPayload(completed?.PayloadJson);
                    if (completedPayload?.Status != QuotationSendConfirmationPayload.ConfirmedStatus
                        || completedPayload.QuotationId != quotationId || completedPayload.RequestMessageId != requestMessageId.Value)
                        return OperationResult.Fail("The send confirmation request is invalid or has been superseded.");
                }
                return OperationResult.Ok("Quotation was already marked as sent.");
            }

            if (!requestMessageId.HasValue && quotation.Status == QuotationStatus.PendingSendConfirmation)
                return OperationResult.Ok("Quotation is already awaiting send confirmation.");

            if (requestMessageId.HasValue
                ? quotation.Status != QuotationStatus.PendingSendConfirmation
                : !QuotationWorkflowRules.CanMarkSent(quotation.Status))
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

            // Confirmation accepts the stored customer offer; standard-price changes do not revoke it.
            if (!requestMessageId.HasValue && await RejectedPricesUnchangedAsync(quotation, cancellationToken))
                return QuotationConfirmationFailure.Create(QuotationSendRejectionRules.PricesUnchangedCode,
                    "Revise customer prices, commission or quantity tiers before resubmitting a rejected send request.");
            var comparison = requestMessageId.HasValue
                ? Array.Empty<QuotationSendPriceSegment>()
                : await ComparePricesAsync(quotation, cancellationToken);
            var needsConfirmation = !requestMessageId.HasValue && comparison.Any(x => x.IsBelowReference);
            if (request.Note?.Trim().Length > MaximumReasonLength || (needsConfirmation && string.IsNullOrWhiteSpace(request.Note)))
                return OperationResult.Fail($"Note is required for below-standard pricing and cannot exceed {MaximumReasonLength} characters.");
            InternalMessage? pendingMessage = null;
            QuotationSendConfirmationPayload? pendingPayload = null;
            if (requestMessageId.HasValue)
            {
                pendingMessage = await LoadPendingMessageAsync(quotation, requestMessageId.Value, cancellationToken);
                pendingPayload = ReadPayload(pendingMessage?.PayloadJson);
                if (pendingMessage is null || pendingPayload is null || pendingPayload.Status != QuotationSendConfirmationPayload.PendingStatus || pendingPayload.QuotationId != quotation.QuotationId
                    || pendingPayload.RequestMessageId != requestMessageId.Value
                    || pendingPayload.ConversationId != pendingMessage.InternalConversationId
                    || pendingPayload.ExpectedUpdatedDate != quotation.UpdatedDate)
                    return confirmationOnly
                        ? QuotationConfirmationFailure.Create(QuotationConfirmationErrors.RequestOutdated, "The send confirmation request is invalid or has already been processed.")
                        : OperationResult.Fail("The send confirmation request is invalid or has already been processed.");
                var blocked = QuotationConfirmationCommercialRules.BlockedReason(quotation, pendingPayload);
                if (blocked is not null)
                    return confirmationOnly
                        ? QuotationConfirmationFailure.Create(blocked, "The customer commercial terms changed or the stored request is invalid. Review and submit a new request.")
                        : OperationResult.Fail("The customer commercial terms changed or the stored request is invalid. Review and submit a new request.");
            }

            var recipientEmployeeIds = needsConfirmation
                ? await _saleGroupRecipientResolver.ResolveActiveEmployeeIdsByRolesAsync(quotation.CompanyId,
                    ApplicationRoleSets.Quotations.SendConfirmers.Select(x => x.ToUpperInvariant()).ToArray(), Guid.Empty, cancellationToken)
                : await ResolveManagementRecipientsAsync(
                quotation.CompanyId,
                quotation.SaleEmployeeId,
                scope.EmployeeId,
                cancellationToken);
            if (pendingMessage is not null)
                recipientEmployeeIds = recipientEmployeeIds.Append(pendingMessage.SenderEmployeeId).Distinct().ToArray();

            if (recipientEmployeeIds.Count == 0)
            {
                return OperationResult.Fail(
                    needsConfirmation ? "No active board approver was found in the current company."
                        : "No active management recipient was found in the current company.");
            }

            var now = _dateTimeProvider.Now;
            var actorName = await _internalMailDbContext.Employees
                .AsNoTracking()
                .Where(x => x.EmployeeId == scope.EmployeeId && x.CompanyId == quotation.CompanyId)
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

            if (needsConfirmation)
                return await RequestConfirmationAsync(quotation, request, comparison!, recipientEmployeeIds,
                    conversation, participantIds, scope.EmployeeId, actorName, now, cancellationToken);

            if (pendingMessage is not null && pendingPayload is not null)
            {
                pendingPayload.Status = QuotationSendConfirmationPayload.ConfirmedStatus;
                pendingPayload.IsUrgent = false;
                pendingPayload.ConfirmedBy = scope.EmployeeId;
                pendingPayload.ConfirmedAt = now;
                pendingMessage.PayloadJson = JsonSerializer.Serialize(pendingPayload, PayloadJsonOptions);
                pendingMessage.IsUrgent = false;
                await ResolvePendingNotificationsAsync(quotation.CompanyId, quotation.QuotationId, pendingMessage.InternalMessageId, pendingPayload, cancellationToken);
            }

            return await CompleteSentAsync(quotation, previousStatus, request.InteractionType, request.Note,
                conversation, participantIds, recipientEmployeeIds, scope.EmployeeId, actorName, now, cancellationToken);
        }

    }
}
