using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateLead;

/// <summary>
/// Feature CRM CustomerCare - xử lý chỉnh sửa thông tin lead trong visibility scope hiện tại.
/// Handler kiểm tra lead thuộc company/current scope, chống sửa address/contact/note chéo customer,
/// patch các field được gửi và giữ route lead không được convert trực tiếp.
/// </summary>
internal sealed class UpdateLeadCommandHandler : IRequestHandler<UpdateLeadCommand, OperationResult>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CustomerTaxCodeConflictService _taxCodeConflictService;

    public UpdateLeadCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider,
        CustomerTaxCodeConflictService taxCodeConflictService)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
        _taxCodeConflictService = taxCodeConflictService;
    }

    public async Task<OperationResult> Handle(UpdateLeadCommand command, CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
        {
            return OperationResult.Fail("CustomerId is invalid.");
        }

        var request = command.Request;
        if (request.CustomerName is not null && string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return OperationResult.Fail("CustomerName cannot be empty.");
        }

        if (request.LeadStatus is LeadStatus.Converted)
        {
            return OperationResult.Fail("LeadStatus cannot be Converted from the lead update endpoint.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var canUpdate = await _visibilityService
            .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
            .AnyAsync(customer => customer.CustomerId == command.CustomerId && customer.IsLead, cancellationToken);
        if (!canUpdate)
        {
            return OperationResult.Fail("Lead was not found or is outside your visibility scope.");
        }

        var customer = await _writeDbContext.Customers
            .Include(item => item.Addresses)
            .Include(item => item.Contacts)
            .FirstOrDefaultAsync(item =>
                item.CustomerId == command.CustomerId &&
                item.CompanyId == scope.CompanyId &&
                item.IsLead,
                cancellationToken);
        if (customer is null)
        {
            return OperationResult.Fail("Lead was not found.");
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
        ApplyLeadFields(customer, request);
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
            return OperationResult.Fail(BuildConcurrencyMessage("Lead", exception));
        }

        return OperationResult.Ok("Lead updated successfully.");
    }

    private static void ApplyLeadFields(Customer customer, UpdateLeadRequest request)
    {
        PatchHelper.SetTrimmed(request.CustomerName, () => customer.CustomerName, value => customer.CustomerName = value ?? string.Empty);
        PatchHelper.SetTrimmed(request.CustomerGroup, () => customer.CustomerGroup, value => customer.CustomerGroup = value);
        PatchHelper.SetTrimmed(request.ApplicationName, () => customer.ApplicationName, value => customer.ApplicationName = value);
        PatchHelper.SetTrimmed(request.RegistrationNumber, () => customer.RegistrationNumber, value => customer.RegistrationNumber = value);
        PatchHelper.SetTrimmed(request.RegistrationAddress, () => customer.RegistrationAddress, value => customer.RegistrationAddress = value);
        PatchHelper.SetTrimmed(request.TaxNumber, () => customer.TaxNumber, value => customer.TaxNumber = value);
        PatchHelper.SetTrimmed(request.Phone, () => customer.Phone, value => customer.Phone = value);
        PatchHelper.SetTrimmed(request.Website, () => customer.Website, value => customer.Website = value);
        if (request.IssueDate.HasValue)
        {
            PatchHelper.SetNullable(request.IssueDate, () => customer.IssueDate, value => customer.IssueDate = value);
        }

        PatchHelper.SetTrimmed(request.IssuedPlace, () => customer.IssuedPlace, value => customer.IssuedPlace = value);
        PatchHelper.SetTrimmed(request.FaxNumber, () => customer.FaxNumber, value => customer.FaxNumber = value);
        if (request.IsActive.HasValue)
        {
            PatchHelper.SetNullable(request.IsActive, () => customer.IsActive, value => customer.IsActive = value);
        }

        PatchHelper.SetIfHasValue(request.LeadStatus, () => customer.LeadStatus, value => customer.LeadStatus = value);
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

    private static string? ValidateChildIds(Customer customer, UpdateLeadRequest request)
    {
        if (request.Addresses is not null)
        {
            var addressIds = request.Addresses.Where(item => item.AddressId != Guid.Empty).Select(item => item.AddressId).ToList();
            if (addressIds.Count != addressIds.Distinct().Count()) return "Addresses contain duplicate AddressId values.";
            if (addressIds.Any(id => customer.Addresses.All(address => address.AddressId != id)))
                return "An address does not belong to this lead.";
        }

        if (request.Contacts is not null)
        {
            var contactIds = request.Contacts.Where(item => item.ContactId != Guid.Empty).Select(item => item.ContactId).ToList();
            if (contactIds.Count != contactIds.Distinct().Count()) return "Contacts contain duplicate ContactId values.";
            if (contactIds.Any(id => customer.Contacts.All(contact => contact.ContactId != id)))
                return "A contact does not belong to this lead.";
        }

        return null;
    }

    private async Task SyncAddressesAsync(
        Customer customer,
        IReadOnlyList<UpdateCustomerAddressRequest>? requests,
        CancellationToken cancellationToken)
    {
        if (requests is null) return;

        Guid? preferredPrimaryId = null;
        foreach (var request in requests)
        {
            var address = request.AddressId == Guid.Empty
                ? new Address
                {
                    AddressId = Guid.CreateVersion7(),
                    CustomerId = customer.CustomerId,
                    IsActive = request.IsActive ?? true
                }
                : customer.Addresses.First(item => item.AddressId == request.AddressId);

            if (request.AddressId == Guid.Empty)
            {
                customer.Addresses.Add(address);
                await _writeDbContext.Addresses.AddAsync(address, cancellationToken);
            }

            PatchHelper.SetTrimmed(request.AddressLine, () => address.AddressLine, value => address.AddressLine = value);
            PatchHelper.SetTrimmed(request.City, () => address.City, value => address.City = value);
            PatchHelper.SetTrimmed(request.District, () => address.District, value => address.District = value);
            PatchHelper.SetTrimmed(request.Province, () => address.Province, value => address.Province = value);
            PatchHelper.SetTrimmed(request.Country, () => address.Country, value => address.Country = value);
            PatchHelper.SetTrimmed(request.PostalCode, () => address.PostalCode, value => address.PostalCode = value);
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
        if (requests is null) return;

        Guid? preferredPrimaryId = null;
        foreach (var request in requests)
        {
            var contact = request.ContactId == Guid.Empty
                ? new Contact
                {
                    ContactId = Guid.CreateVersion7(),
                    CustomerId = customer.CustomerId,
                    IsActive = request.IsActive ?? true
                }
                : customer.Contacts.First(item => item.ContactId == request.ContactId);

            if (request.ContactId == Guid.Empty)
            {
                customer.Contacts.Add(contact);
                await _writeDbContext.Contacts.AddAsync(contact, cancellationToken);
            }

            PatchHelper.SetTrimmed(request.FirstName, () => contact.FirstName, value => contact.FirstName = value);
            PatchHelper.SetTrimmed(request.LastName, () => contact.LastName, value => contact.LastName = value);
            PatchHelper.SetTrimmed(request.Gender, () => contact.Gender, value => contact.Gender = value);
            PatchHelper.SetTrimmed(request.Phone, () => contact.Phone, value => contact.Phone = value);
            PatchHelper.SetTrimmed(request.Email, () => contact.Email, value => contact.Email = value);
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

    private Task<Guid?> ResolveCurrentGroupIdAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
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
