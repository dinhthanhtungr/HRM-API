using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomerInteraction;

internal sealed class UpdateCustomerInteractionCommandHandler
    : IRequestHandler<UpdateCustomerInteractionCommand, OperationResult>
{
    private const string DefaultFollowUpTitle = "Customer follow-up";
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateCustomerInteractionCommandHandler(
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

    public async Task<OperationResult> Handle(
        UpdateCustomerInteractionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.InteractionId == Guid.Empty)
        {
            return OperationResult.Fail("Interaction id is invalid.");
        }

        var request = command.Request;
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var visibleCustomerIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        var canUpdate = await _readDbContext.CustomerInteractions
            .AsTracking()
            .AnyAsync(x =>
                x.Id == command.InteractionId &&
                x.CompanyId == scope.CompanyId &&
                visibleCustomerIds.Contains(x.CustomerId),
                cancellationToken);

        if (!canUpdate)
        {
            return OperationResult.Fail("Interaction was not found or is outside your scope.");
        }

        var interaction = await _writeDbContext.CustomerInteractions
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == command.InteractionId &&
                x.CompanyId == scope.CompanyId,
                cancellationToken);

        if (interaction is null)
        {
            return OperationResult.Fail("Interaction was not found.");
        }

        if (request.ContactId.HasValue &&
            !await ContactBelongsToCustomerAsync(request.ContactId.Value, interaction.CustomerId, cancellationToken))
        {
            return OperationResult.Fail("Contact does not belong to this customer.");
        }

        if (request.AssignedSaleEmployeeId.HasValue)
        {
            var employee = await _accessService.ResolveEmployeeAsync(scope, request.AssignedSaleEmployeeId, false, cancellationToken);
            if (!employee.IsAllowed)
            {
                return OperationResult.Fail("Assigned sale employee is outside your scope.");
            }

            PatchHelper.SetNullable(employee.EmployeeId, () => interaction.AssignedSaleEmployeeId, value => interaction.AssignedSaleEmployeeId = value);
        }

        if (request.InteractionType.HasValue && !Enum.IsDefined(request.InteractionType.Value))
        {
            return OperationResult.Fail("Interaction type is invalid.");
        }

        if (request.Content is { } content)
        {
            if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > CustomerCrmTaskRules.MaxBodyLength)
            {
                return OperationResult.Fail("Interaction content is invalid.");
            }

            PatchHelper.SetTrimmed(content, () => interaction.Content, value => interaction.Content = value ?? string.Empty);
        }

        if (request.ContactId.HasValue)
        {
            PatchHelper.SetNullable(request.ContactId, () => interaction.ContactId, value => interaction.ContactId = value);
        }
        PatchHelper.SetIfHasValue(request.InteractionType, () => interaction.InteractionType, value => interaction.InteractionType = value);
        PatchHelper.SetTrimmed(request.Subject, () => interaction.Subject, value => interaction.Subject = value);
        PatchHelper.SetTrimmed(request.Outcome, () => interaction.Outcome, value => interaction.Outcome = value);
        PatchHelper.SetTrimmed(request.NextAction, () => interaction.NextAction, value => interaction.NextAction = value);
        PatchHelper.SetIfHasValue(request.InteractionAt, () => interaction.InteractionAt, value => interaction.InteractionAt = value);
        if (request.NextFollowUpDate.HasValue)
        {
            PatchHelper.SetNullable(request.NextFollowUpDate, () => interaction.NextFollowUpDate, value => interaction.NextFollowUpDate = value);
        }
        PatchHelper.SetIfHasValue(request.IsActive, () => interaction.IsActive, value => interaction.IsActive = value);
        interaction.UpdatedDate = _dateTimeProvider.Now;
        interaction.UpdatedBy = scope.EmployeeId;

        var customer = await _writeDbContext.Customers.FirstAsync(x => x.CustomerId == interaction.CustomerId, cancellationToken);
        var previousLastContact = await _readDbContext.CustomerInteractions.AsNoTracking()
            .Where(x => x.CustomerId == interaction.CustomerId && x.CompanyId == scope.CompanyId && x.IsActive && x.Id != interaction.Id)
            .MaxAsync(x => (DateTime?)x.InteractionAt, cancellationToken);
        customer.LastContactDate = interaction.IsActive
            ? !previousLastContact.HasValue || interaction.InteractionAt > previousLastContact ? interaction.InteractionAt : previousLastContact
            : previousLastContact;
        customer.CurrentSaleId = interaction.AssignedSaleEmployeeId;

        var linkedTask = await _writeDbContext.WorkTasks
            .FirstOrDefaultAsync(x =>
                x.CompanyId == scope.CompanyId &&
                x.References.Any(reference =>
                    reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                    reference.ReferenceId == interaction.Id),
                cancellationToken);

        if (linkedTask is not null && CustomerCrmTaskRules.IsOpen(linkedTask.Status))
        {
            PatchHelper.SetTrimmed(request.NextAction, () => linkedTask.NextAction, value => linkedTask.NextAction = value);
            if (request.Content is not null)
            {
                PatchHelper.Set(interaction.Content, () => linkedTask.Description, value => linkedTask.Description = value);
            }
            if (request.NextFollowUpDate.HasValue && linkedTask.DueDate != request.NextFollowUpDate)
            {
                PatchHelper.SetNullable(request.NextFollowUpDate, () => linkedTask.DueDate, value => linkedTask.DueDate = value);
                linkedTask.DueReminderSentAt = null;
            }

            PatchHelper.SetNullable(interaction.AssignedSaleEmployeeId, () => linkedTask.AssignedToEmployeeId, value => linkedTask.AssignedToEmployeeId = value);
            if (interaction.AssignedSaleEmployeeId.HasValue)
            {
                await EnsurePrimaryTaskAssigneeAsync(linkedTask.Id, interaction.AssignedSaleEmployeeId.Value, scope.EmployeeId, cancellationToken);
            }

            linkedTask.UpdatedDate = _dateTimeProvider.Now;
            linkedTask.UpdatedBy = scope.EmployeeId;
        }
        else if (linkedTask is null && request.NextFollowUpDate.HasValue && interaction.AssignedSaleEmployeeId.HasValue)
        {
            await CreateLinkedTaskAsync(customer, interaction, request.NextFollowUpDate.Value, interaction.AssignedSaleEmployeeId.Value, _dateTimeProvider.Now, cancellationToken);
        }



        customer.NextFollowUpDate = await ResolveNextFollowUpAsync(
            customer.CustomerId,
            scope.CompanyId,
            linkedTask?.Id,
            linkedTask is not null && linkedTask.IsActive && CustomerCrmTaskRules.IsOpen(linkedTask.Status)
                ? linkedTask.DueDate
                : request.NextFollowUpDate,
            cancellationToken);
        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
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

    private async Task EnsurePrimaryTaskAssigneeAsync(
        Guid taskId,
        Guid employeeId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var currentPrimary = await _writeDbContext.WorkTaskAssignees
            .Where(x => x.WorkTaskId == taskId && x.IsActive && x.IsPrimary)
            .ToListAsync(cancellationToken);
        foreach (var item in currentPrimary) item.IsPrimary = false;

        var assignee = await _writeDbContext.WorkTaskAssignees
            .OrderByDescending(x => x.IsActive)
            .FirstOrDefaultAsync(x => x.WorkTaskId == taskId && x.EmployeeId == employeeId, cancellationToken);
        if (assignee is null)
        {
            await _writeDbContext.WorkTaskAssignees.AddAsync(new WorkTaskAssignee
            {
                Id = Guid.CreateVersion7(), WorkTaskId = taskId, EmployeeId = employeeId,
                IsPrimary = true, IsActive = true, CreatedDate = _dateTimeProvider.Now, CreatedBy = actorId
            }, cancellationToken);
        }
        else
        {
            assignee.IsActive = true;
            assignee.IsPrimary = true;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
