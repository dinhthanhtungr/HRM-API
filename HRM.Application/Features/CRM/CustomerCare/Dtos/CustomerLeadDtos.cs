using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

public sealed class UpdateLeadRequest : CustomerProfilePatchRequest
{
}

public sealed class ClaimLeadRequest
{
    public Guid? EmployeeId { get; init; }
    public Guid? GroupId { get; init; }
    public int ClaimTtlDays { get; init; } = 365;
}

/// <summary>
/// Contract nội bộ còn được giữ cho command chuyển lead; CRM controller không public action này cho FE.
/// </summary>
public sealed class ConvertLeadRequest
{
    public Guid? EmployeeId { get; init; }
    public Guid? GroupId { get; init; }
}
