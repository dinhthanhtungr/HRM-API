using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.Notifications
{
    public class NotificationRecipient
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid NotificationId { get; set; }

        // Chỉ được phép 1 trong 3 cột có giá trị (check constraint ở Fluent)
        public Guid? TargetUserId { get; set; }      // gửi cá nhân
        public string? TargetRole { get; set; }      // gửi theo role, ví dụ "Board"
        public Guid? TargetTeamId { get; set; }      // gửi theo team/phòng ban

        public Employee TargetUserNavigation { get; set; } = default!;
        public Part TargetTeamNavigation { get; set; } = default!;
        public Notification Notification { get; set; } = default!;
    }
}
