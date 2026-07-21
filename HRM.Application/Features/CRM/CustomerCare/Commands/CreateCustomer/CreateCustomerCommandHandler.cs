using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomer;

internal sealed class CreateCustomerCommandHandler
    : IRequestHandler<CreateCustomerCommand, OperationResult<CustomerCreateResultDto>>
{
    private const int MinimumClaimTtlHours = 1;
    private const int MaximumClaimTtlHours = 8760;

    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IExternalIdService _externalIdService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CustomerTaxCodeConflictService _taxCodeConflictService;

    public CreateCustomerCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IExternalIdService externalIdService,
        IDateTimeProvider dateTimeProvider,
        CustomerTaxCodeConflictService taxCodeConflictService)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _externalIdService = externalIdService;
        _dateTimeProvider = dateTimeProvider;
        _taxCodeConflictService = taxCodeConflictService;
    }

    public async Task<OperationResult<CustomerCreateResultDto>> Handle(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return OperationResult<CustomerCreateResultDto>.Fail("CustomerName is required.");
        }

        if (request.ClaimTtlHours is < MinimumClaimTtlHours or > MaximumClaimTtlHours)
        {
            return OperationResult<CustomerCreateResultDto>.Fail(
                $"ClaimTtlHours must be between {MinimumClaimTtlHours} and {MaximumClaimTtlHours}.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var groupId = await _readDbContext.MemberInGroups
            .AsNoTracking()
            .Where(member =>
                member.Profile == scope.EmployeeId &&
                member.IsActive &&
                member.Group.CompanyId == scope.CompanyId)
            .OrderByDescending(member => member.IsAdmin == true)
            .ThenBy(member => member.MemberId)
            .Select(member => (Guid?)member.GroupId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!groupId.HasValue)
        {
            return OperationResult<CustomerCreateResultDto>.Fail(
                "Current sale employee does not belong to an active group in this company.");
        }

        var taxConflict = await _taxCodeConflictService.FindBlockingConflictAsync(
            scope.CompanyId,
            request.TaxNumber,
            excludedCustomerId: null,
            cancellationToken);
        if (taxConflict is not null)
        {
            return OperationResult<CustomerCreateResultDto>.Fail(
                CustomerTaxCodeConflictService.BuildConflictMessage(request.TaxNumber, taxConflict));
        }

        var externalId = string.IsNullOrWhiteSpace(request.ExternalId)
            ? await _externalIdService.GenerateGlobalCodeAsync(
                scope.CompanyId,
                DocumentPrefix.KH.ToString(),
                cancellationToken)
            : request.ExternalId.Trim();

        var externalIdExists = await _readDbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer =>
                customer.CompanyId == scope.CompanyId &&
                customer.ExternalId == externalId,
                cancellationToken);
        if (externalIdExists)
        {
            return OperationResult<CustomerCreateResultDto>.Fail(
                $"Customer external id {externalId} already exists in this company.");
        }

        var now = _dateTimeProvider.Now;
        var customerId = Guid.CreateVersion7();
        var customer = new Customer
        {
            CustomerId = customerId,
            ExternalId = externalId,
            CustomerName = request.CustomerName.Trim(),
            CustomerGroup = CustomerProfileRules.TrimToNull(request.CustomerGroup),
            ApplicationName = CustomerProfileRules.TrimToNull(request.ApplicationName),
            RegistrationNumber = CustomerProfileRules.TrimToNull(request.RegistrationNumber),
            RegistrationAddress = CustomerProfileRules.TrimToNull(request.RegistrationAddress),
            TaxNumber = CustomerProfileRules.TrimToNull(request.TaxNumber),
            Phone = CustomerProfileRules.TrimToNull(request.Phone),
            Website = CustomerProfileRules.TrimToNull(request.Website),
            IssueDate = request.IssueDate,
            IssuedPlace = CustomerProfileRules.TrimToNull(request.IssuedPlace),
            FaxNumber = CustomerProfileRules.TrimToNull(request.FaxNumber),
            CompanyId = scope.CompanyId,
            CreatedBy = scope.EmployeeId,
            CreatedDate = now,
            UpdatedBy = scope.EmployeeId,
            UpdatedDate = now,
            IsActive = true,
            IsLead = true,
            LeadStatus = LeadStatus.Claimed,
            CurrentSaleId = null
        };

        foreach (var addressRequest in request.Addresses ?? Array.Empty<CreateCustomerAddressRequest>())
        {
            customer.Addresses.Add(new Address
            {
                AddressId = Guid.CreateVersion7(),
                CustomerId = customerId,
                AddressLine = CustomerProfileRules.TrimToNull(addressRequest.AddressLine),
                City = CustomerProfileRules.TrimToNull(addressRequest.City),
                District = CustomerProfileRules.TrimToNull(addressRequest.District),
                Province = CustomerProfileRules.TrimToNull(addressRequest.Province),
                Country = CustomerProfileRules.TrimToNull(addressRequest.Country),
                PostalCode = CustomerProfileRules.TrimToNull(addressRequest.PostalCode),
                IsPrimary = addressRequest.IsPrimary,
                IsActive = true
            });
        }

        foreach (var contactRequest in request.Contacts ?? Array.Empty<CreateCustomerContactRequest>())
        {
            customer.Contacts.Add(new Contact
            {
                ContactId = Guid.CreateVersion7(),
                CustomerId = customerId,
                FirstName = CustomerProfileRules.TrimToNull(contactRequest.FirstName),
                LastName = CustomerProfileRules.TrimToNull(contactRequest.LastName),
                Gender = CustomerProfileRules.TrimToNull(contactRequest.Gender),
                Phone = CustomerProfileRules.TrimToNull(contactRequest.Phone),
                Email = CustomerProfileRules.TrimToNull(contactRequest.Email),
                IsPrimary = contactRequest.IsPrimary,
                IsActive = true
            });
        }

        CustomerProfileRules.NormalizePrimaryAddresses(customer.Addresses);
        CustomerProfileRules.NormalizePrimaryContacts(customer.Contacts);
        await _writeDbContext.Customers.AddAsync(customer, cancellationToken);

        var claimExpiresAt = now.AddHours(request.ClaimTtlHours);
        await _writeDbContext.CustomerClaims.AddAsync(new CustomerClaim
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            EmployeeId = scope.EmployeeId,
            GroupId = groupId.Value,
            Type = ClaimType.Work,
            ExpiresAt = claimExpiresAt,
            IsActive = true,
            CompanyId = scope.CompanyId
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            await _writeDbContext.CustomerNotes.AddAsync(new CustomerNote
            {
                Id = Guid.CreateVersion7(),
                CustomerId = customerId,
                AuthorEmployeeId = scope.EmployeeId,
                AuthorGroupId = groupId.Value,
                Content = request.Notes.Trim(),
                Visibility = NoteVisibility.Group,
                IsApprovedShare = false,
                CreatedAt = now,
                CompanyId = scope.CompanyId
            }, cancellationToken);
        }

        await _writeDbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<CustomerCreateResultDto>.Ok(new CustomerCreateResultDto
        {
            CustomerId = customerId,
            ExternalId = externalId,
            IsLead = customer.IsLead,
            LeadStatus = customer.LeadStatus.ToString(),
            CurrentSaleId = customer.CurrentSaleId,
            ClaimExpiresAt = claimExpiresAt
        }, "Lead customer created and claimed successfully.");
    }

}
