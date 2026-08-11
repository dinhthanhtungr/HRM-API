using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;

internal sealed class CreateSampleTrialInteractionCommandHandler
    : IRequestHandler<CreateSampleTrialInteractionCommand, OperationResult<Guid>>
{
    private const string DefaultSubject = "Sample trial update";
    private const string DefaultFollowUpTitle = "Sample trial follow-up";
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateSampleTrialInteractionCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
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

        var scope = await _accessService.BuildScopeAsync(cancellationToken);
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

        var now = _dateTimeProvider.Now;
        var interactionAt = request.InteractionAt == default ? now : request.InteractionAt;
        var subject = Normalize(request.Subject) ?? BuildDefaultSubject(trial);
        var interactionId = Guid.CreateVersion7();
        var interaction = new CustomerInteraction
        {
            Id = interactionId,
            CustomerId = customer.CustomerId,
            ContactId = request.ContactId,
            InteractionType = CustomerInteractionType.SampleTrial,
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

        ApplyCustomerReply(trial, request, interactionAt, scope.EmployeeId, now);
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

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<Guid>.Ok(interactionId);
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
            .FirstOrDefaultAsync(x =>
                x.SampleRequestSampleTrialId == sampleTrialId &&
                x.IsActive &&
                x.SampleRequest.CustomerId == customerId &&
                x.SampleRequest.CompanyId == companyId &&
                x.SampleRequest.IsActive,
                cancellationToken);

    private static void ApplyCustomerReply(
        SampleRequestSampleTrial trial,
        CreateSampleTrialInteractionRequest request,
        DateTime interactionAt,
        Guid employeeId,
        DateTime now)
    {
        var replyStatus = Normalize(request.CustomerReplyStatus);
        if (replyStatus is not null)
        {
            trial.CustomerReplyStatus = replyStatus;
        }

        trial.CustomerReplyDate = request.CustomerReplyDate ?? interactionAt;
        trial.CustomerReplyByEmployeeId = employeeId;
        trial.CustomerReplyNote = Normalize(request.CustomerReplyNote) ?? request.Content.Trim();
        trial.OrderDate = request.OrderDate ?? trial.OrderDate;
        trial.UpdatedDate = now;
        trial.UpdatedBy = employeeId;
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
}
