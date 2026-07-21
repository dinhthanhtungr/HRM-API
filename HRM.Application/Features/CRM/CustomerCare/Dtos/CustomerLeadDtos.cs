using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

public sealed class UpdateLeadRequest
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
    public LeadStatus? LeadStatus { get; init; }
    public UpdateCustomerNoteRequest? Note { get; init; }
    public IReadOnlyList<UpdateCustomerAddressRequest>? Addresses { get; init; }
    public IReadOnlyList<UpdateCustomerContactRequest>? Contacts { get; init; }
}

public sealed class ClaimLeadRequest
{
    public Guid? EmployeeId { get; init; }
    public Guid? GroupId { get; init; }
    public int ClaimTtlDays { get; init; } = 365;
}

public sealed class ConvertLeadRequest
{
    public Guid? EmployeeId { get; init; }
    public Guid? GroupId { get; init; }
}
