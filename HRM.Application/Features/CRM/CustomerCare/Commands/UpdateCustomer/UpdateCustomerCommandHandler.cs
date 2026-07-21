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

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomer;

/// <summary>
/// Feature CRM CustomerCare - xử lý cập nhật hồ sơ khách hàng cho sale/leader/director.
/// Handler validate input, kiểm tra visibility theo company/current employee, chặn sửa
/// address/contact/note không thuộc customer hiện tại, rồi patch profile và child records.
/// Side effect chính là ghi DB: cập nhật customer, chuẩn hóa primary address/contact,
/// chuyển lead thành customer có assignment khi hợp lệ và tạo/cập nhật note group.
/// </summary>
internal sealed class UpdateCustomerCommandHandler
    : IRequestHandler<UpdateCustomerCommand, OperationResult>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CustomerTaxCodeConflictService _taxCodeConflictService;

    public UpdateCustomerCommandHandler(
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

    public async Task<OperationResult> Handle(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
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

        if (request.LeadStatus.HasValue && !Enum.IsDefined(request.LeadStatus.Value))
        {
            return OperationResult.Fail("LeadStatus is invalid.");
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
            .Include(item => item.CustomerAssignments)
            .Include(item => item.CustomerClaims)
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

        if (request.IsLead == true && customer.CustomerAssignments.Any(assignment => assignment.IsActive))
        {
            return OperationResult.Fail("Customer cannot be changed back to lead while an active assignment exists.");
        }

        var childValidationError = ValidateChildIds(customer, request);
        if (childValidationError is not null)
        {
            return OperationResult.Fail(childValidationError);
        }

        var now = _dateTimeProvider.Now;
        customer.UpdatedBy = scope.EmployeeId;
        customer.UpdatedDate = now;
        ApplyBasicFields(customer, request);

        PatchHelper.SetIfHasValue(request.IsLead, () => customer.IsLead, value => customer.IsLead = value);
        PatchHelper.SetIfHasValue(request.LeadStatus, () => customer.LeadStatus, value => customer.LeadStatus = value);

        if (!customer.IsLead && !customer.CustomerAssignments.Any(assignment => assignment.IsActive))
        {
            var groupId = await ResolveCurrentGroupIdAsync(scope.CompanyId, scope.EmployeeId, cancellationToken);
            if (!groupId.HasValue)
            {
                return OperationResult.Fail("Current sale employee does not belong to an active group in this company.");
            }

            await _writeDbContext.CustomerAssignments.AddAsync(new CustomerAssignment
            {
                Id = Guid.CreateVersion7(),
                CustomerId = customer.CustomerId,
                EmployeeId = scope.EmployeeId,
                GroupId = groupId.Value,
                CompanyId = scope.CompanyId,
                CreatedBy = scope.EmployeeId,
                CreatedDate = now,
                UpdatedBy = scope.EmployeeId,
                UpdatedDate = now,
                IsActive = true
            }, cancellationToken);

            foreach (var claim in customer.CustomerClaims.Where(claim => claim.IsActive && claim.Type == ClaimType.Work))
            {
                claim.IsActive = false;
            }

            customer.CurrentSaleId = scope.EmployeeId;
            if (!request.LeadStatus.HasValue)
            {
                customer.LeadStatus = LeadStatus.Converted;
            }
        }

        if (!customer.IsLead)
        {
            var latestAssignment = customer.CustomerAssignments
                .Where(assignment => assignment.IsActive)
                .OrderByDescending(assignment => assignment.CreatedDate)
                .FirstOrDefault();
            if (latestAssignment is not null)
            {
                customer.CurrentSaleId = latestAssignment.EmployeeId;
            }

            if (request.IsLead == false && !request.LeadStatus.HasValue)
            {
                customer.LeadStatus = LeadStatus.Converted;
            }
        }

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

    private static void ApplyBasicFields(Customer customer, UpdateCustomerRequest request)
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
