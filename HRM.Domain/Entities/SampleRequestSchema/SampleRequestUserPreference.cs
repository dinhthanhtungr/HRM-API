using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.SampleRequestSchema;

/// <summary>
/// Ghim yêu cầu mẫu riêng của từng nhân viên; không thay đổi trạng thái TP hoặc VU.
/// Khi đăng ký EF cần khóa duy nhất CompanyId + EmployeeId + SampleRequestId.
/// </summary>
public sealed class SampleRequestUserPreference
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid SampleRequestId { get; set; }
    public bool IsPinned { get; set; }
    public bool NotifyOnDue { get; set; }
    public Guid? ReminderTaskId { get; set; }
    public DateTime? PinnedAt { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public Company Company { get; set; } = default!;
    public Employee Employee { get; set; } = default!;
    public SampleRequest SampleRequest { get; set; } = default!;
}
