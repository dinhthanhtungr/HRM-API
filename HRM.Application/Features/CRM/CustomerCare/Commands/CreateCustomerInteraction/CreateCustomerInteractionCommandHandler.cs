using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomerInteraction;

internal sealed class CreateCustomerInteractionCommandHandler
    : IRequestHandler<CreateCustomerInteractionCommand, OperationResult<Guid>>
{
    private const string DefaultFollowUpTitle = "Customer follow-up";
    private const int MaxReferenceCount = 10;
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateCustomerInteractionCommandHandler(
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
        CreateCustomerInteractionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Content) ||
            request.Content.Trim().Length > CustomerCrmTaskRules.MaxBodyLength)
        {
            return OperationResult<Guid>.Fail("Customer and interaction content are required.");
        }

        if (!Enum.IsDefined(request.InteractionType))
        {
            return OperationResult<Guid>.Fail("Interaction type is invalid.");
        }

        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var customer = await GetVisibleCustomerForUpdateAsync(request.CustomerId, scope.CompanyId, scope, cancellationToken);
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

        var now = _dateTimeProvider.Now;
        var referencesResult = await ResolveReferencesAsync(
            request.References,
            customer.CustomerId,
            scope.CompanyId,
            cancellationToken);
        if (!referencesResult.Success || referencesResult.Data is null)
        {
            return OperationResult<Guid>.Fail(referencesResult.Message ?? "Interaction references are invalid.");
        }

        var interactionAt = request.InteractionAt == default ? now : request.InteractionAt;
        var interactionId = Guid.CreateVersion7();
        var interaction = new CustomerInteraction
        {
            Id = interactionId,
            CustomerId = customer.CustomerId,
            ContactId = request.ContactId,
            InteractionType = request.InteractionType,
            Subject = Normalize(request.Subject),
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
        foreach (var reference in referencesResult.Data)
        {
            await _writeDbContext.CustomerInteractionReferences.AddAsync(new CustomerInteractionReference
            {
                Id = Guid.CreateVersion7(),
                InteractionId = interactionId,
                ReferenceType = reference.ReferenceType,
                ReferenceId = reference.ReferenceId,
                ReferenceCodeSnapshot = reference.ReferenceCodeSnapshot,
                ReferenceNameSnapshot = reference.ReferenceNameSnapshot,
                IsPrimary = reference.IsPrimary,
                CompanyId = scope.CompanyId,
                CreatedDate = now,
                CreatedBy = scope.EmployeeId
            }, cancellationToken);
        }

        customer.LastContactDate = !customer.LastContactDate.HasValue || interactionAt > customer.LastContactDate
            ? interactionAt
            : customer.LastContactDate;
        customer.CurrentSaleId = employee.EmployeeId;

        if (request.NextFollowUpDate.HasValue)
        {
            await CreateLinkedTaskAsync(customer, interaction, request.NextFollowUpDate.Value, employee.EmployeeId.Value, now, cancellationToken);
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
                Id = Guid.CreateVersion7(), WorkTaskId = taskId, ReferenceType = WorkReferenceType.Customer,
                ReferenceId = customer.CustomerId, ReferenceCodeSnapshot = customer.ExternalId,
                ReferenceNameSnapshot = customer.CustomerName, IsPrimary = true
            },
            new WorkTaskReference
            {
                Id = Guid.CreateVersion7(), WorkTaskId = taskId, ReferenceType = WorkReferenceType.CustomerInteraction,
                ReferenceId = interaction.Id, ReferenceCodeSnapshot = interaction.Subject, IsPrimary = false
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
            x.CompanyId == companyId && x.IsActive && x.DueDate.HasValue &&
            x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled &&
            x.References.Any(reference => reference.ReferenceType == WorkReferenceType.Customer && reference.ReferenceId == customerId));
        if (excludedTaskId.HasValue) query = query.Where(x => x.Id != excludedTaskId.Value);
        var existing = await query.MinAsync(x => x.DueDate, cancellationToken);
        if (!candidateDueDate.HasValue) return existing;
        return !existing.HasValue || candidateDueDate < existing ? candidateDueDate : existing;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<OperationResult<List<ReferenceDraft>>> ResolveReferencesAsync(
        IReadOnlyList<CustomerInteractionReferenceRequest> references,
        Guid customerId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (references.Count == 0)
        {
            return OperationResult<List<ReferenceDraft>>.Ok([]);
        }

        if (references.Count > MaxReferenceCount)
        {
            return OperationResult<List<ReferenceDraft>>.Fail($"Interaction references cannot exceed {MaxReferenceCount} items.");
        }

        var duplicated = references
            .GroupBy(x => new { x.ReferenceType, x.ReferenceId })
            .Any(x => x.Key.ReferenceId == Guid.Empty || x.Count() > 1);
        if (duplicated)
        {
            return OperationResult<List<ReferenceDraft>>.Fail("Interaction references contain empty or duplicate values.");
        }

        if (references.Count(x => x.IsPrimary) > 1)
        {
            return OperationResult<List<ReferenceDraft>>.Fail("Only one interaction reference can be primary.");
        }

        var result = new List<ReferenceDraft>();
        foreach (var reference in references)
        {
            if (!Enum.IsDefined(reference.ReferenceType))
            {
                return OperationResult<List<ReferenceDraft>>.Fail("Interaction reference type is invalid.");
            }

            var resolved = await ResolveReferenceAsync(reference, customerId, companyId, cancellationToken);
            if (resolved is null)
            {
                return OperationResult<List<ReferenceDraft>>.Fail(
                    $"Reference {reference.ReferenceType}/{reference.ReferenceId} was not found or is outside this customer.");
            }

            result.Add(resolved);
        }

        if (result.Count > 0 && result.All(x => !x.IsPrimary))
        {
            result[0] = result[0] with { IsPrimary = true };
        }

        return OperationResult<List<ReferenceDraft>>.Ok(result);
    }

    private async Task<ReferenceDraft?> ResolveReferenceAsync(
        CustomerInteractionReferenceRequest reference,
        Guid customerId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return reference.ReferenceType switch
        {
            CustomerInteractionReferenceType.SampleRequest => await _readDbContext.SampleRequests
                .AsNoTracking()
                .Where(x =>
                    x.SampleRequestId == reference.ReferenceId &&
                    x.CustomerId == customerId &&
                    x.CompanyId == companyId &&
                    x.IsActive)
                .Select(x => new ReferenceDraft(
                    reference.ReferenceType,
                    x.SampleRequestId,
                    Normalize(reference.ReferenceCodeSnapshot) ?? x.ExternalId,
                    Normalize(reference.ReferenceNameSnapshot) ?? x.Product.Name,
                    reference.IsPrimary))
                .FirstOrDefaultAsync(cancellationToken),

            CustomerInteractionReferenceType.SampleTrial => await _readDbContext.SampleRequestSampleTrials
                .AsNoTracking()
                .Where(x =>
                    x.SampleRequestSampleTrialId == reference.ReferenceId &&
                    x.IsActive &&
                    x.SampleRequest.CustomerId == customerId &&
                    x.SampleRequest.CompanyId == companyId &&
                    x.SampleRequest.IsActive)
                .Select(x => new ReferenceDraft(
                    reference.ReferenceType,
                    x.SampleRequestSampleTrialId,
                    Normalize(reference.ReferenceCodeSnapshot) ??
                    x.SampleRequestExternalIdSnapshot ??
                    x.SampleRequest.ExternalId,
                    Normalize(reference.ReferenceNameSnapshot) ??
                    x.ProductNameSnapshot ??
                    x.SampleRequest.Product.Name,
                    reference.IsPrimary))
                .FirstOrDefaultAsync(cancellationToken),

            CustomerInteractionReferenceType.Quotation => await _readDbContext.Quotations
                .AsNoTracking()
                .Where(x =>
                    x.QuotationId == reference.ReferenceId &&
                    x.CustomerId == customerId &&
                    x.CompanyId == companyId &&
                    x.IsActive)
                .Select(x => new ReferenceDraft(
                    reference.ReferenceType,
                    x.QuotationId,
                    Normalize(reference.ReferenceCodeSnapshot) ?? x.ExternalId,
                    Normalize(reference.ReferenceNameSnapshot) ?? x.Status.ToString(),
                    reference.IsPrimary))
                .FirstOrDefaultAsync(cancellationToken),

            _ => null
        };
    }

    private sealed record ReferenceDraft(
        CustomerInteractionReferenceType ReferenceType,
        Guid ReferenceId,
        string? ReferenceCodeSnapshot,
        string? ReferenceNameSnapshot,
        bool IsPrimary);
}
