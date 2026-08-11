using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomer;

/// <summary>
/// Cập nhật hồ sơ khách hàng cho CustomerEditors trong đúng company/visibility scope.
/// Handler chỉ patch profile, address, contact và note; không cho FE đổi lifecycle IsLead/LeadStatus/IsActive.
/// Field null là không đổi, còn xóa field nullable phải dùng clearFields được whitelist.
/// </summary>
internal sealed class UpdateCustomerCommandHandler
    : IRequestHandler<UpdateCustomerCommand, OperationResult>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CustomerTaxCodeConflictService _taxCodeConflictService;
    private readonly ICurrentUser _currentUser;

    public UpdateCustomerCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider,
        CustomerTaxCodeConflictService taxCodeConflictService,
        ICurrentUser currentUser)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
        _taxCodeConflictService = taxCodeConflictService;
        _currentUser = currentUser;
    }

    public async Task<OperationResult> Handle(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
        {
            return OperationResult.Fail("CustomerId is invalid.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.CRM.CustomerEditors))
        {
            return OperationResult.Fail("You are not allowed to update customers.");
        }

        var request = command.Request;
        var patchValidationError = CustomerProfilePatchRules.Validate(request);
        if (patchValidationError is not null)
        {
            return OperationResult.Fail(patchValidationError);
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var canUpdate = await _visibilityService
            .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
            .AnyAsync(customer => customer.CustomerId == command.CustomerId, cancellationToken);
        if (!canUpdate)
        {
            return OperationResult.Fail("Customer was not found or is outside your visibility scope.");
        }

        var customer = await _writeDbContext.Customers
            .Include(item => item.Addresses)
            .Include(item => item.Contacts)
            .FirstOrDefaultAsync(item =>
                item.CustomerId == command.CustomerId &&
                item.CompanyId == scope.CompanyId,
                cancellationToken);
        if (customer is null)
        {
            return OperationResult.Fail("Customer was not found.");
        }

        if (request.TaxNumber is not null)
        {
            var conflict = await _taxCodeConflictService.FindBlockingConflictAsync(
                scope.CompanyId,
                request.TaxNumber,
                customer.CustomerId,
                cancellationToken);
            if (conflict is not null)
            {
                return OperationResult.Fail(
                    CustomerTaxCodeConflictService.BuildConflictMessage(request.TaxNumber, conflict));
            }
        }

        var childValidationError = ValidateChildIds(customer, request);
        if (childValidationError is not null)
        {
            return OperationResult.Fail(childValidationError);
        }

        var now = _dateTimeProvider.Now;
        customer.UpdatedBy = scope.EmployeeId;
        customer.UpdatedDate = now;
        CustomerProfilePatchRules.ApplyCustomerPatch(customer, request);

        await SyncAddressesAsync(customer, request.Addresses, cancellationToken);
        await SyncContactsAsync(customer, request.Contacts, cancellationToken);

        var noteResult = await UpsertNoteAsync(customer, request.Note, scope.CompanyId, scope.EmployeeId, now, cancellationToken);
        if (!noteResult.Success)
        {
            return noteResult;
        }

        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult.Fail(BuildConcurrencyMessage("Customer", exception));
        }

        return OperationResult.Ok("Customer updated successfully.");
    }

    private static string BuildConcurrencyMessage(string featureName, DbUpdateConcurrencyException exception)
    {
        var entityNames = exception.Entries
            .Select(entry => entry.Entity.GetType().Name)
            .Distinct()
            .ToList();

        var suffix = entityNames.Count == 0
            ? string.Empty
            : $" Affected entities: {string.Join(", ", entityNames)}.";

        return $"{featureName} data was changed or deleted by another process. Please reload before saving.{suffix}";
    }

    private static string? ValidateChildIds(Customer customer, UpdateCustomerRequest request)
    {
        if (request.Addresses is not null)
        {
            var addressIds = request.Addresses.Where(item => item.AddressId != Guid.Empty).Select(item => item.AddressId).ToList();
            if (addressIds.Count != addressIds.Distinct().Count()) return "Addresses contain duplicate AddressId values.";
            if (addressIds.Any(id => customer.Addresses.All(address => address.AddressId != id)))
                return "An address does not belong to this customer.";
        }

        if (request.Contacts is not null)
        {
            var contactIds = request.Contacts.Where(item => item.ContactId != Guid.Empty).Select(item => item.ContactId).ToList();
            if (contactIds.Count != contactIds.Distinct().Count()) return "Contacts contain duplicate ContactId values.";
            if (contactIds.Any(id => customer.Contacts.All(contact => contact.ContactId != id)))
                return "A contact does not belong to this customer.";
        }

        return null;
    }

    private async Task SyncAddressesAsync(
        Customer customer,
        IReadOnlyList<UpdateCustomerAddressRequest>? requests,
        CancellationToken cancellationToken)
    {
        if (requests is null)
        {
            return;
        }

        Guid? preferredPrimaryId = null;
        foreach (var request in requests)
        {
            Address address;
            if (request.AddressId == Guid.Empty)
            {
                address = new Address
                {
                    AddressId = Guid.CreateVersion7(),
                    CustomerId = customer.CustomerId,
                    IsActive = request.IsActive ?? true
                };
                customer.Addresses.Add(address);
                await _writeDbContext.Addresses.AddAsync(address, cancellationToken);
            }
            else
            {
                address = customer.Addresses.First(item => item.AddressId == request.AddressId);
            }

            CustomerProfilePatchRules.ApplyAddressPatch(address, request);
            PatchHelper.SetIfHasValue(request.IsActive, () => address.IsActive, value => address.IsActive = value);
            if (request.IsPrimary.HasValue)
            {
                PatchHelper.SetNullable(request.IsPrimary, () => address.IsPrimary, value => address.IsPrimary = value);
            }

            if (!address.IsActive) address.IsPrimary = false;
            if (address.IsActive && request.IsPrimary == true) preferredPrimaryId = address.AddressId;
        }

        CustomerProfileRules.NormalizePrimaryAddresses(customer.Addresses, preferredPrimaryId);
    }

    private async Task SyncContactsAsync(
        Customer customer,
        IReadOnlyList<UpdateCustomerContactRequest>? requests,
        CancellationToken cancellationToken)
    {
        if (requests is null)
        {
            return;
        }

        Guid? preferredPrimaryId = null;
        foreach (var request in requests)
        {
            Contact contact;
            if (request.ContactId == Guid.Empty)
            {
                contact = new Contact
                {
                    ContactId = Guid.CreateVersion7(),
                    CustomerId = customer.CustomerId,
                    IsActive = request.IsActive ?? true
                };
                customer.Contacts.Add(contact);
                await _writeDbContext.Contacts.AddAsync(contact, cancellationToken);
            }
            else
            {
                contact = customer.Contacts.First(item => item.ContactId == request.ContactId);
            }

            CustomerProfilePatchRules.ApplyContactPatch(contact, request);
            PatchHelper.SetIfHasValue(request.IsActive, () => contact.IsActive, value => contact.IsActive = value);
            if (request.IsPrimary.HasValue)
            {
                PatchHelper.SetNullable(request.IsPrimary, () => contact.IsPrimary, value => contact.IsPrimary = value);
            }

            if (!contact.IsActive) contact.IsPrimary = false;
            if (contact.IsActive && request.IsPrimary == true) preferredPrimaryId = contact.ContactId;
        }

        CustomerProfileRules.NormalizePrimaryContacts(customer.Contacts, preferredPrimaryId);
    }

    private async Task<OperationResult> UpsertNoteAsync(
        Customer customer,
        UpdateCustomerNoteRequest? request,
        Guid companyId,
        Guid employeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Content))
        {
            return OperationResult.Ok();
        }

        if (request.NoteId is { } noteId && noteId != Guid.Empty)
        {
            var note = await _writeDbContext.CustomerNotes.FirstOrDefaultAsync(item =>
                item.Id == noteId &&
                item.CustomerId == customer.CustomerId &&
                item.CompanyId == companyId &&
                item.AuthorEmployeeId == employeeId,
                cancellationToken);
            if (note is null)
            {
                return OperationResult.Fail("Note was not found or was not created by the current employee.");
            }

            note.Content = request.Content.Trim();
            note.CreatedAt = now;
            return OperationResult.Ok();
        }

        var groupId = await ResolveCurrentGroupIdAsync(companyId, employeeId, cancellationToken);
        if (!groupId.HasValue)
        {
            return OperationResult.Fail("Current sale employee does not belong to an active group in this company.");
        }

        await _writeDbContext.CustomerNotes.AddAsync(new CustomerNote
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customer.CustomerId,
            AuthorEmployeeId = employeeId,
            AuthorGroupId = groupId.Value,
            Content = request.Content.Trim(),
            Visibility = NoteVisibility.Group,
            IsApprovedShare = false,
            CreatedAt = now,
            CompanyId = companyId
        }, cancellationToken);
        return OperationResult.Ok();
    }

    private Task<Guid?> ResolveCurrentGroupIdAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
        => _readDbContext.MemberInGroups
            .AsNoTracking()
            .Where(member =>
                member.Profile == employeeId &&
                member.IsActive &&
                member.Group.CompanyId == companyId)
            .OrderByDescending(member => member.IsAdmin == true)
            .ThenBy(member => member.MemberId)
            .Select(member => (Guid?)member.GroupId)
            .FirstOrDefaultAsync(cancellationToken);
}
