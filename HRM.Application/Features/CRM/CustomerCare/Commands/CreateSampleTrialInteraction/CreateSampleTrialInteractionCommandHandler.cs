using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;

internal sealed class CreateSampleTrialInteractionCommandHandler
    : IRequestHandler<CreateSampleTrialInteractionCommand, OperationResult<Guid>>
{
    private const string DefaultSubject = "Cập nhật phản hồi mẫu";
    private const string DefaultFollowUpTitle = "Sample trial follow-up";
    private const int MaxCustomerReplyStatusLength = 50;
    private const int MaxCustomerReplyNoteLength = 5000;
    private const int MaxMessageDetailLength = 1000;
    private const string ApprovedReplyStatus = "APPROVED";
    private const string FailedReplyStatus = "FAIL";
    private const string CancelledReplyStatus = "CANCEL";
    private const string WaitingReplyStatus = "WAITING";
    private const string PriceQuoteReplyStatus = "BÁO GIÁ";
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISender _sender;

    public CreateSampleTrialInteractionCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider,
        ISender sender)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
        _sender = sender;
    }

    public async Task<OperationResult<Guid>> Handle(
        CreateSampleTrialInteractionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.CustomerId == Guid.Empty ||
            request.SampleRequestSampleTrialId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Content) ||
            request.Content.Trim().Length > CustomerCrmTaskRules.MaxBodyLength)
        {
            return OperationResult<Guid>.Fail("Customer, sample trial and interaction content are required.");
        }

        if (!Enum.IsDefined(request.InteractionType))
        {
            return OperationResult<Guid>.Fail("InteractionType is invalid.");
        }

        var replyStatus = Normalize(request.CustomerReplyStatus);
        var replyNote = Normalize(request.CustomerReplyNote);
        if (replyStatus is { Length: > MaxCustomerReplyStatusLength } ||
            request.IdempotencyKey.HasValue && replyStatus is null)
        {
            return OperationResult<Guid>.Fail(
                $"CustomerReplyStatus is required and cannot exceed {MaxCustomerReplyStatusLength} characters.");
        }

        if (replyNote is { Length: > MaxCustomerReplyNoteLength })
        {
            return OperationResult<Guid>.Fail(
                $"CustomerReplyNote cannot exceed {MaxCustomerReplyNoteLength} characters.");
        }

        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var interactionId = request.IdempotencyKey.HasValue && request.IdempotencyKey.Value != Guid.Empty
            ? SampleTrialInteractionIdempotency.CreateInteractionId(scope.CompanyId, request.IdempotencyKey.Value)
            : Guid.CreateVersion7();

        if (request.IdempotencyKey == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("IdempotencyKey cannot be empty when supplied.");
        }

        var customer = await GetVisibleCustomerForUpdateAsync(
            request.CustomerId,
            scope.CompanyId,
            scope,
            cancellationToken);
        if (customer is null)
        {
            return OperationResult<Guid>.Fail("Customer was not found or is outside your scope.");
        }

        if (request.ContactId.HasValue &&
            !await ContactBelongsToCustomerAsync(request.ContactId.Value, customer.CustomerId, cancellationToken))
        {
            return OperationResult<Guid>.Fail("Contact does not belong to this customer.");
        }

        var employee = await _accessService.ResolveEmployeeAsync(
            scope,
            request.AssignedSaleEmployeeId,
            defaultToCurrent: true,
            cancellationToken);
        if (!employee.IsAllowed || !employee.EmployeeId.HasValue)
        {
            return OperationResult<Guid>.Fail("Assigned sale employee is outside your scope.");
        }

        var trial = await GetSampleTrialForUpdateAsync(
            request.SampleRequestSampleTrialId,
            customer.CustomerId,
            scope.CompanyId,
            cancellationToken);
        if (trial is null)
        {
            return OperationResult<Guid>.Fail("Sample trial was not found or is outside this customer.");
        }

        if (request.IdempotencyKey.HasValue)
        {
            var replayResult = await ResolveIdempotentReplayAsync(
                interactionId,
                request.CustomerId,
                request.SampleRequestSampleTrialId,
                scope.CompanyId,
                cancellationToken);
            if (replayResult is not null)
            {
                return replayResult;
            }
        }

        request.ExpectedTrialUpdatedDate = NormalizeDatabaseTimestamp(request.ExpectedTrialUpdatedDate);
        request.CustomerReplyDate = NormalizeDatabaseTimestamp(request.CustomerReplyDate);
        request.OrderDate = NormalizeDatabaseTimestamp(request.OrderDate);
        request.NextFollowUpDate = NormalizeDatabaseTimestamp(request.NextFollowUpDate);

        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDateWithDatabasePrecision(
            request.ExpectedTrialUpdatedDate,
            trial.UpdatedDate,
            "Sample trial");
        if (concurrencyError is not null)
        {
            return OperationResult<Guid>.Fail(
                "Sample trial was changed by another user. Reload before saving feedback.");
        }

        var now = NormalizeDatabaseTimestamp(_dateTimeProvider.Now);
        var oldSampleRequestStatus = trial.SampleRequest.Status;
        var lifecycleValidationError = await ApplyCustomerFeedbackLifecycleAsync(
            trial,
            replyStatus!,
            scope.EmployeeId,
            now,
            cancellationToken);
        if (lifecycleValidationError is not null)
        {
            return OperationResult<Guid>.Fail(lifecycleValidationError);
        }

        var interactionAt = request.InteractionAt == default
            ? now
            : NormalizeDatabaseTimestamp(request.InteractionAt);
        if (request.IdempotencyKey.HasValue &&
            (interactionAt > now.AddMinutes(5) || request.CustomerReplyDate > now.AddMinutes(5)))
        {
            return OperationResult<Guid>.Fail("InteractionAt and CustomerReplyDate cannot be in the future.");
        }

        if (request.IdempotencyKey.HasValue &&
            request.NextFollowUpDate.HasValue &&
            request.NextFollowUpDate.Value < interactionAt)
        {
            return OperationResult<Guid>.Fail("NextFollowUpDate cannot be earlier than InteractionAt.");
        }

        var subject = Normalize(request.Subject) ?? BuildDefaultSubject(trial);
        var interaction = new CustomerInteraction
        {
            Id = interactionId,
            CustomerId = customer.CustomerId,
            ContactId = request.ContactId,
            InteractionType = request.InteractionType,
            Subject = subject,
            Content = request.Content.Trim(),
            Outcome = Normalize(request.Outcome),
            NextAction = Normalize(request.NextAction),
            InteractionAt = interactionAt,
            NextFollowUpDate = request.NextFollowUpDate,
            AssignedSaleEmployeeId = employee.EmployeeId,
            CompanyId = scope.CompanyId,
            CreatedDate = now,
            CreatedBy = scope.EmployeeId,
            IsActive = true
        };

        await _writeDbContext.CustomerInteractions.AddAsync(interaction, cancellationToken);
        await _writeDbContext.CustomerInteractionReferences.AddAsync(new CustomerInteractionReference
        {
            Id = Guid.CreateVersion7(),
            InteractionId = interactionId,
            ReferenceType = CustomerInteractionReferenceType.SampleTrial,
            ReferenceId = trial.SampleRequestSampleTrialId,
            ReferenceCodeSnapshot = trial.SampleRequestExternalIdSnapshot ?? trial.SampleRequest.ExternalId,
            ReferenceNameSnapshot = trial.ProductNameSnapshot ?? trial.SampleRequest.Product.Name,
            IsPrimary = true,
            CompanyId = scope.CompanyId,
            CreatedDate = now,
            CreatedBy = scope.EmployeeId
        }, cancellationToken);

        SampleTrialInteractionMutation.ApplyCustomerReply(
            trial,
            request,
            interactionAt,
            scope.EmployeeId,
            now);
        await SampleRequestDataChangeAuditHelper.AddStatusTransitionAuditIfChangedAsync(
            _writeDbContext.AuditLogs,
            trial.SampleRequest,
            oldSampleRequestStatus,
            scope.EmployeeId,
            now,
            "CustomerCareSampleTrialInteraction",
            cancellationToken);
        customer.LastContactDate = !customer.LastContactDate.HasValue || interactionAt > customer.LastContactDate
            ? interactionAt
            : customer.LastContactDate;
        customer.CurrentSaleId = employee.EmployeeId;

        if (request.NextFollowUpDate.HasValue)
        {
            await CreateLinkedTaskAsync(
                customer,
                interaction,
                request.NextFollowUpDate.Value,
                employee.EmployeeId.Value,
                now,
                cancellationToken);
            customer.NextFollowUpDate = await ResolveNextFollowUpAsync(
                customer.CustomerId,
                scope.CompanyId,
                excludedTaskId: null,
                request.NextFollowUpDate,
                cancellationToken);
        }

        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);

            var messageResult = await SendCustomerFeedbackMessageAsync(
                trial,
                replyStatus!,
                replyNote,
                request.Content,
                cancellationToken);
            if (!messageResult.Success)
            {
                return OperationResult<Guid>.Ok(
                    interactionId,
                    $"Recorded customer feedback successfully, but could not send notification: {messageResult.Message}");
            }

            return OperationResult<Guid>.Ok(interactionId);
        }
        catch (DbUpdateException ex) when (request.IdempotencyKey.HasValue)
        {
            _writeDbContext.ClearTrackedChanges();

            var replayResult = await ResolveIdempotentReplayAsync(
                interactionId,
                request.CustomerId,
                request.SampleRequestSampleTrialId,
                scope.CompanyId,
                cancellationToken);

            if (replayResult is not null)
            {
                return replayResult;
            }

            throw new InvalidOperationException(
                $"Could not save customer feedback: {ex.InnerException?.Message ?? ex.Message}",
                ex);
        }
    }

    private async Task<OperationResult<Guid>?> ResolveIdempotentReplayAsync(
        Guid interactionId,
        Guid customerId,
        Guid trialId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var existing = await _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x => x.Id == interactionId)
            .Select(x => new
            {
                x.CustomerId,
                x.CompanyId,
                HasMatchingTrial = x.References.Any(reference =>
                    reference.ReferenceType == CustomerInteractionReferenceType.SampleTrial &&
                    reference.ReferenceId == trialId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        return existing.CompanyId == companyId &&
               existing.CustomerId == customerId &&
               existing.HasMatchingTrial
            ? OperationResult<Guid>.Ok(interactionId, "Customer feedback interaction was already recorded.")
            : OperationResult<Guid>.Fail("IdempotencyKey was already used for a different interaction.");
    }

    private async Task<Customer?> GetVisibleCustomerForUpdateAsync(
        Guid customerId,
        Guid companyId,
        ViewerScope scope,
        CancellationToken cancellationToken)
    {
        var visibleCustomerIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);
        return await _writeDbContext.Customers.FirstOrDefaultAsync(x =>
            x.CustomerId == customerId &&
            x.CompanyId == companyId &&
            x.IsActive == true &&
            visibleCustomerIds.Contains(x.CustomerId), cancellationToken);
    }

    private Task<bool> ContactBelongsToCustomerAsync(Guid contactId, Guid customerId, CancellationToken cancellationToken)
        => _readDbContext.Contacts.AsNoTracking().AnyAsync(x =>
            x.ContactId == contactId && x.CustomerId == customerId && x.IsActive, cancellationToken);

    private Task<SampleRequestSampleTrial?> GetSampleTrialForUpdateAsync(
        Guid sampleTrialId,
        Guid customerId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _writeDbContext.SampleRequestSampleTrials
            .AsTracking()
            .Include(x => x.SampleRequest)
            .ThenInclude(x => x.Product)
            .Include(x => x.Formula)
            .FirstOrDefaultAsync(x =>
                x.SampleRequestSampleTrialId == sampleTrialId &&
                x.IsActive &&
                x.SampleRequest.CustomerId == customerId &&
                x.SampleRequest.CompanyId == companyId &&
                x.SampleRequest.IsActive,
                cancellationToken);

    private async Task<string?> ApplyCustomerFeedbackLifecycleAsync(
        SampleRequestSampleTrial trial,
        string replyStatus,
        Guid employeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = replyStatus.Trim().ToUpperInvariant();
        if (normalizedStatus == ApprovedReplyStatus)
        {
            var approvalValidationError = SampleRequestSampleTrialApprovalRules.Validate(
                trial,
                trial.FormulaId ?? Guid.Empty);
            if (approvalValidationError is not null)
            {
                return approvalValidationError;
            }

            var productFormulas = await _writeDbContext.Formulas
                .Where(x =>
                    x.ProductId == trial.SampleRequest.ProductId &&
                    x.CompanyId == trial.SampleRequest.CompanyId &&
                    x.IsActive)
                .ToListAsync(cancellationToken);

            SampleRequestSampleTrialApprovalRules.ApplyApproved(
                trial.SampleRequest,
                trial,
                productFormulas,
                employeeId,
                now,
                ApprovedReplyStatus);
            return null;
        }

        if (normalizedStatus == FailedReplyStatus)
        {
            if (trial.Formula is null)
            {
                return "Sample trial does not have a formula to mark as failed.";
            }

            trial.Status = SampleTrialStatus.Failed;
            trial.Formula.Status = FormulaStatus.Rejected.ToString();
            trial.Formula.UpdatedBy = employeeId;
            trial.Formula.UpdatedDate = now;
            SampleRequestStatusTransitionRules.MarkCustomerFailed(trial.SampleRequest);
            trial.SampleRequest.UpdatedBy = employeeId;
            trial.SampleRequest.UpdatedDate = now;
            return null;
        }

        if (normalizedStatus == CancelledReplyStatus)
        {
            trial.Status = SampleTrialStatus.Cancelled;
            if (trial.Formula is not null)
            {
                trial.Formula.Status = FormulaStatus.Cancelled.ToString();
                trial.Formula.UpdatedBy = employeeId;
                trial.Formula.UpdatedDate = now;
            }

            SampleRequestStatusTransitionRules.MarkCustomerCancelled(trial.SampleRequest);
            trial.SampleRequest.UpdatedBy = employeeId;
            trial.SampleRequest.UpdatedDate = now;
            return null;
        }

        if (normalizedStatus == WaitingReplyStatus)
        {
            trial.Status = SampleTrialStatus.WaitingCustomerFeedback;
            return null;
        }

        if (string.Equals(replyStatus.Trim(), PriceQuoteReplyStatus, StringComparison.OrdinalIgnoreCase))
        {
            trial.Status = SampleTrialStatus.PriceQuote;
        }

        return null;
    }

    private Task<OperationResult<SendInternalMessageResultDto>> SendCustomerFeedbackMessageAsync(
        SampleRequestSampleTrial trial,
        string customerReplyStatus,
        string? customerReplyNote,
        string interactionContent,
        CancellationToken cancellationToken)
    {
        var sampleRequestExternalId = trial.SampleRequestExternalIdSnapshot ?? trial.SampleRequest.ExternalId;
        var formulaExternalId = trial.Formula?.ExternalId ?? trial.BatchNo ?? "--";
        var outcome = trial.Status switch
        {
            SampleTrialStatus.Approved => "Khách hàng đã chấp nhận mẫu. Công thức đã hoàn thành.",
            SampleTrialStatus.Failed => "Khách hàng chưa đạt mẫu. Lab vui lòng phát triển và gửi lại mẫu.",
            SampleTrialStatus.Cancelled => "Khách hàng đã dừng hoặc từ chối yêu cầu mẫu.",
            SampleTrialStatus.PriceQuote => "Sale đã ghi nhận khách hàng yêu cầu báo giá.",
            SampleTrialStatus.WaitingCustomerFeedback => "Sale đã ghi nhận mẫu đang chờ phản hồi khách hàng.",
            _ => "Sale đã cập nhật phản hồi khách hàng."
        };
        var detail = TruncateMessageDetail(customerReplyNote ?? interactionContent);

        return _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = trial.SampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Phản hồi khách hàng cho yêu cầu phối mẫu {sampleRequestExternalId}, lần thử {trial.TrialNo}. " +
                      $"Công thức: {formulaExternalId}. Trạng thái: {customerReplyStatus}. {outcome}" +
                      (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Ghi chú: {detail}"),
            TopicOverride = TopicNotifications.SampleRequestCustomerFeedbackRecorded,
            TitleOverride = "Phản hồi khách hàng về mẫu đã gửi"
        }, cancellationToken);
    }

    private static string? TruncateMessageDetail(string? value)
    {
        var normalized = Normalize(value);
        return normalized is null || normalized.Length <= MaxMessageDetailLength
            ? normalized
            : $"{normalized[..(MaxMessageDetailLength - 3)]}...";
    }

    private async Task CreateLinkedTaskAsync(
        Customer customer,
        CustomerInteraction interaction,
        DateTime dueDate,
        Guid assignedEmployeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var taskId = Guid.CreateVersion7();
        var task = new WorkTask
        {
            Id = taskId,
            Title = Normalize(interaction.NextAction) ?? Normalize(interaction.Subject) ?? DefaultFollowUpTitle,
            Description = interaction.Content,
            NextAction = interaction.NextAction,
            Status = WorkTaskStatus.Pending,
            Priority = WorkTaskPriority.Normal,
            DueDate = dueDate,
            AssignedToEmployeeId = assignedEmployeeId,
            CompanyId = interaction.CompanyId,
            CreatedDate = now,
            CreatedBy = interaction.CreatedBy,
            IsActive = true
        };

        await _writeDbContext.WorkTasks.AddAsync(task, cancellationToken);
        await _writeDbContext.WorkTaskAssignees.AddAsync(new WorkTaskAssignee
        {
            Id = Guid.CreateVersion7(),
            WorkTaskId = taskId,
            EmployeeId = assignedEmployeeId,
            IsPrimary = true,
            IsActive = true,
            CreatedDate = now,
            CreatedBy = interaction.CreatedBy
        }, cancellationToken);
        await _writeDbContext.WorkTaskReferences.AddRangeAsync(new[]
        {
            new WorkTaskReference
            {
                Id = Guid.CreateVersion7(),
                WorkTaskId = taskId,
                ReferenceType = WorkReferenceType.Customer,
                ReferenceId = customer.CustomerId,
                ReferenceCodeSnapshot = customer.ExternalId,
                ReferenceNameSnapshot = customer.CustomerName,
                IsPrimary = true
            },
            new WorkTaskReference
            {
                Id = Guid.CreateVersion7(),
                WorkTaskId = taskId,
                ReferenceType = WorkReferenceType.CustomerInteraction,
                ReferenceId = interaction.Id,
                ReferenceCodeSnapshot = interaction.Subject,
                IsPrimary = false
            }
        }, cancellationToken);
    }

    private async Task<DateTime?> ResolveNextFollowUpAsync(
        Guid customerId,
        Guid companyId,
        Guid? excludedTaskId,
        DateTime? candidateDueDate,
        CancellationToken cancellationToken)
    {
        var query = _readDbContext.WorkTasks.AsNoTracking().Where(x =>
            x.CompanyId == companyId &&
            x.IsActive &&
            x.DueDate.HasValue &&
            x.Status != WorkTaskStatus.Done &&
            x.Status != WorkTaskStatus.Canceled &&
            x.References.Any(reference =>
                reference.ReferenceType == WorkReferenceType.Customer &&
                reference.ReferenceId == customerId));
        if (excludedTaskId.HasValue)
        {
            query = query.Where(x => x.Id != excludedTaskId.Value);
        }

        var existing = await query.MinAsync(x => x.DueDate, cancellationToken);
        if (!candidateDueDate.HasValue)
        {
            return existing;
        }

        return !existing.HasValue || candidateDueDate < existing ? candidateDueDate : existing;
    }

    private static string BuildDefaultSubject(SampleRequestSampleTrial trial)
    {
        var code = Normalize(trial.SampleRequestExternalIdSnapshot) ?? Normalize(trial.SampleRequest.ExternalId);
        var name = Normalize(trial.ProductNameSnapshot) ?? Normalize(trial.SampleRequest.Product.Name);
        if (code is not null && name is not null)
        {
            return $"{DefaultSubject}: {code} - {name}";
        }

        return code is not null ? $"{DefaultSubject}: {code}" : DefaultSubject;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeDatabaseTimestamp(DateTime value)
        => value.Kind == DateTimeKind.Unspecified
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime? NormalizeDatabaseTimestamp(DateTime? value)
        => value.HasValue ? NormalizeDatabaseTimestamp(value.Value) : null;
}
