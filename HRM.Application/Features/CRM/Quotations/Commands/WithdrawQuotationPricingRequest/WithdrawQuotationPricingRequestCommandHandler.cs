using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.WithdrawQuotationPricing;

internal sealed class WithdrawQuotationPricingRequestCommandHandler
    : IRequestHandler<WithdrawQuotationPricingRequestCommand,
        OperationResult<QuotationStatusTransitionDto>>
{
    private const int MaximumReasonLength = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly KeyedMutationLock<Guid> _mutationLock;

    public WithdrawQuotationPricingRequestCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        IInternalMailDbContext internalMailDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider,
        KeyedMutationLock<Guid> mutationLock)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _internalMailDbContext = internalMailDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
        _mutationLock = mutationLock;
    }

    public async Task<OperationResult<QuotationStatusTransitionDto>> Handle(
        WithdrawQuotationPricingRequestCommand command,
        CancellationToken cancellationToken)
    {
        var reason = QuotationRules.TrimToNull(command.Request.Reason);
        if (command.QuotationId == Guid.Empty ||
            !command.Request.ExpectedUpdatedDate.HasValue ||
            reason is null ||
            reason.Length > MaximumReasonLength)
        {
            return OperationResult<QuotationStatusTransitionDto>.Fail(
                $"ExpectedUpdatedDate and reason are required; reason cannot exceed {MaximumReasonLength} characters.");
        }

        using var lease = await _mutationLock.AcquireAsync(
            command.QuotationId, cancellationToken);
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _writeDbContext.Quotations
            .AsTracking()
            .Include(x => x.Lines.Where(line => line.IsActive))
            .FirstOrDefaultAsync(x =>
                x.QuotationId == command.QuotationId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive,
                cancellationToken);
        if (quotation is null || !await _visibilityService
                .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken))
        {
            return OperationResult<QuotationStatusTransitionDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        if (!QuotationWorkflowRules.CanWithdrawPricingRequest(quotation.Status))
        {
            return OperationResult<QuotationStatusTransitionDto>.Fail(
                "Only a pending or approved quotation pricing request can be withdrawn.");
        }

        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            command.Request.ExpectedUpdatedDate, quotation.UpdatedDate, "Quotation");
        if (concurrencyError is not null)
        {
            return OperationResult<QuotationStatusTransitionDto>.Fail(concurrencyError);
        }

        var now = _dateTimeProvider.Now;
        var previousStatus = quotation.Status;
        quotation.Status = QuotationStatus.Draft;
        quotation.UpdatedBy = scope.EmployeeId;
        quotation.UpdatedDate = now;
        _writeDbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(),
            QuotationId = quotation.QuotationId,
            FromStatus = previousStatus,
            ToStatus = QuotationStatus.Draft,
            Note = reason,
            ChangedBy = scope.EmployeeId,
            ChangedDate = now
        });

        var conversation = await FindOrCreateConversationAsync(
            quotation, scope.EmployeeId, now, cancellationToken);
        var message = new InternalMessage
        {
            InternalMessageId = Guid.CreateVersion7(),
            InternalConversationId = conversation.InternalConversationId,
            SenderEmployeeId = scope.EmployeeId,
            MessageType = InternalMessageType.System,
            Body = $"Đã thu hồi yêu cầu duyệt giá {quotation.ExternalId}. Lý do: {reason}",
            SentAt = now,
            IsEdited = false,
            IsDeleted = false,
            PayloadJson = JsonSerializer.Serialize(new
            {
                contentType = "QuotationPricingRequestWithdrawn",
                quotationId = quotation.QuotationId,
                quotationExternalId = quotation.ExternalId,
                reason,
                quotationStatus = QuotationStatus.Draft,
                action = new
                {
                    code = "Quotation.Open",
                    parameters = new
                    {
                        quotationId = quotation.QuotationId,
                        quotationExternalId = quotation.ExternalId
                    }
                }
            }, JsonOptions)
        };
        await _internalMailDbContext.InternalMessages.AddAsync(message, cancellationToken);
        await _internalMailDbContext.InternalMessageReferences.AddAsync(
            new InternalMessageReference
            {
                InternalMessageReferenceId = Guid.CreateVersion7(),
                InternalMessageId = message.InternalMessageId,
                RelatedType = InternalMailRelatedType.Quotation,
                RelatedId = quotation.QuotationId,
                RelatedExternalId = quotation.ExternalId,
                RelatedNameSnapshot = conversation.Subject,
                IsPrimary = true
            },
            cancellationToken);
        conversation.LastMessageId = message.InternalMessageId;
        conversation.LastMessageAt = now;

        var participantIds = await _internalMailDbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => x.InternalConversationId == conversation.InternalConversationId && x.IsActive)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        foreach (var employeeId in participantIds.Append(scope.EmployeeId).Distinct())
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
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<QuotationStatusTransitionDto>.Fail(
                OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
        }

        return OperationResult<QuotationStatusTransitionDto>.Ok(
            new QuotationStatusTransitionDto
            {
                QuotationId = quotation.QuotationId,
                Status = quotation.Status,
                UpdatedDate = quotation.UpdatedDate
            },
            "Quotation pricing request withdrawn successfully.");
    }

    private async Task<InternalConversation> FindOrCreateConversationAsync(
        Quotation quotation,
        Guid employeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var subject = QuotationConversationSubjectService.BuildSubject(
            quotation.ExternalId,
            quotation.Lines.OrderBy(x => x.SortOrder).Select(x => x.ProductExternalIdSnapshot));
        var conversation = await _internalMailDbContext.InternalConversations
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.CompanyId == quotation.CompanyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.Quotation &&
                x.RelatedId == quotation.QuotationId,
                cancellationToken);
        if (conversation is not null)
        {
            conversation.Subject = subject;
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
            CreatedBy = employeeId,
            CreatedAt = now,
            LastMessageAt = now,
            IsActive = true
        };
        await _internalMailDbContext.InternalConversations.AddAsync(conversation, cancellationToken);
        await _internalMailDbContext.InternalConversationParticipants.AddAsync(
            new InternalConversationParticipant
            {
                InternalConversationId = conversation.InternalConversationId,
                EmployeeId = employeeId,
                Role = InternalConversationParticipantRole.Owner,
                JoinedAt = now,
                LastReadAt = now
            },
            cancellationToken);
        await _internalMailDbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }
}
