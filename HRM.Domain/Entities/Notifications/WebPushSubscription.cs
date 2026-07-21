using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.Notifications;

/// <summary>
/// Dang ky Web Push cua mot trinh duyet/thiet bi cho mot nhan vien.
/// Endpoint va bo khoa subscription la du lieu nhay cam, khong duoc ghi vao log.
/// </summary>
public class WebPushSubscription
{
    public Guid WebPushSubscriptionId { get; set; }

    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }

    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public int FailureCount { get; set; }

    public virtual Company Company { get; set; } = default!;
    public virtual Employee Employee { get; set; } = default!;
}
