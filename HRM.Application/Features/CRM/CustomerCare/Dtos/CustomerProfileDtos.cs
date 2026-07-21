using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

public sealed class CreateCustomerRequest
{
    public string? ExternalId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerGroup { get; init; }
    public string? ApplicationName { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? RegistrationAddress { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public DateTime? IssueDate { get; init; }
    public string? IssuedPlace { get; init; }
    public string? FaxNumber { get; init; }
    public string? Notes { get; init; }
    public int ClaimTtlHours { get; init; } = 48;
    public IReadOnlyList<CreateCustomerAddressRequest> Addresses { get; init; } = [];
    public IReadOnlyList<CreateCustomerContactRequest> Contacts { get; init; } = [];
}

public sealed class UpdateCustomerRequest
{
    public string? CustomerName { get; init; }
    public string? CustomerGroup { get; init; }
    public string? ApplicationName { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? RegistrationAddress { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public DateTime? IssueDate { get; init; }
    public string? IssuedPlace { get; init; }
    public string? FaxNumber { get; init; }
    public bool? IsActive { get; init; }
    public bool? IsLead { get; init; }
    public LeadStatus? LeadStatus { get; init; }
    public UpdateCustomerNoteRequest? Note { get; init; }
    public IReadOnlyList<UpdateCustomerAddressRequest>? Addresses { get; init; }
    public IReadOnlyList<UpdateCustomerContactRequest>? Contacts { get; init; }
}

public sealed class CreateCustomerAddressRequest
{
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }
    public string? Country { get; init; }
    public string? PostalCode { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class UpdateCustomerAddressRequest
{
    public Guid AddressId { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }
    public string? Country { get; init; }
    public string? PostalCode { get; init; }
    public bool? IsPrimary { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class CreateCustomerContactRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Gender { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class UpdateCustomerContactRequest
{
    public Guid ContactId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Gender { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool? IsPrimary { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class UpdateCustomerNoteRequest
{
    public Guid? NoteId { get; init; }
    public string Content { get; init; } = string.Empty;
}

public sealed class CustomerCreateResultDto
{
    public Guid CustomerId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public bool IsLead { get; init; }
    public string LeadStatus { get; init; } = string.Empty;
    public Guid? CurrentSaleId { get; init; }
    public DateTime? ClaimExpiresAt { get; init; }
}

public sealed class CustomerDetailDto
{
    public Guid CustomerId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerGroup { get; init; }
    public string? ApplicationName { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? RegistrationAddress { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public DateTime? IssueDate { get; init; }
    public string? IssuedPlace { get; init; }
    public string? FaxNumber { get; init; }
    public string? CompanyName { get; init; }
    public bool IsLead { get; init; }
    public string LeadStatus { get; init; } = string.Empty;
    public string? CurrentCrmStatus { get; init; }
    public Guid? CurrentSaleId { get; init; }
    public string? CurrentSaleName { get; init; }
    public DateTime? LastContactDate { get; init; }
    public DateTime? NextFollowUpDate { get; init; }
    public DateTime CreatedDate { get; init; }
    public bool? IsActive { get; init; }
    public IReadOnlyList<CustomerAddressDto> Addresses { get; init; } = [];
    public IReadOnlyList<CustomerContactDto> Contacts { get; init; } = [];
    public IReadOnlyList<CustomerNoteDto> Notes { get; init; } = [];
}

public sealed class CustomerAddressDto
{
    public Guid AddressId { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }
    public string? Country { get; init; }
    public string? PostalCode { get; init; }
    public bool? IsPrimary { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CustomerContactDto
{
    public Guid ContactId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Gender { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool? IsPrimary { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CustomerNoteDto
{
    public Guid NoteId { get; init; }
    public string Content { get; init; } = string.Empty;
    public Guid AuthorEmployeeId { get; init; }
    public string AuthorEmployeeName { get; init; } = string.Empty;
    public Guid AuthorGroupId { get; init; }
    public DateTime CreatedAt { get; init; }
}
