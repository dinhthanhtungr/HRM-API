namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

public sealed record SampleSharingCustomerDto(Guid CustomerId, string Code, string Name, string? SaleName = null, string? TaxNumber = null);
public sealed record SampleSharingGroupDto(Guid Id, string Name, IReadOnlyList<SampleSharingCustomerDto> Members, bool CanEdit = true);
public sealed record SampleSharingWorkspaceDto(bool Enabled, bool CanManage, IReadOnlyList<SampleSharingGroupDto> Groups,
    IReadOnlyList<SampleSharingCustomerDto>? Customers = null, IReadOnlyList<Guid>? RestrictedCustomerIds = null,
    string? Revision = null, bool CanAdmin = false);
public sealed record LinkSampleSharingCustomersRequest(Guid OtherCustomerId, string? Name);
public sealed record CreateSampleSharingGroupRequest(string Name);
public sealed record AssignSampleSharingGroupRequest([property: System.Text.Json.Serialization.JsonRequired] Guid? GroupId);
