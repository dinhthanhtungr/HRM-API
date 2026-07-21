using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Domain.Entities.WorkTaskSchema;

/// <summary>
/// Tham chieu mem toi nghiep vu cha. Service phai validate ReferenceId va CompanyId truoc khi ghi.
/// </summary>
public sealed class WorkTaskReference
{
    public Guid Id { get; set; }
    public Guid WorkTaskId { get; set; }
    public WorkReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public string? ReferenceCodeSnapshot { get; set; }
    public string? ReferenceNameSnapshot { get; set; }
    public bool IsPrimary { get; set; }

    public WorkTask WorkTask { get; set; } = default!;
}
