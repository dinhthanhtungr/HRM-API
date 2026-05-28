using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Notifications;

namespace HRM.Domain.Entities.Notifications
{

    public class Notification
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Phân loại/tái sử dụng
        public TopicNotifications Topic { get; set; } = default!;               // ví dụ: "Mfg.PriceExceeded"
        public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

        // Nội dung hiển thị
        public string Title { get; set; } = default!;
        public string Message { get; set; } = default!;
        public string? Link { get; set; }                            // deep-link
        public string? PayloadJson { get; set; }                     // metadata linh hoạt (JSON)

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public Guid CompanyId { get; set; }
        public Guid CreatedBy { get; set; }

        public string? CreatedByNameSnapshot { get; set; }                     // lưu tạm tên người tạo để hiển thị nhanh

        public Employee? CreatedByEmployeeNavigation { get; set; }        
        public Company Company { get; set; } = default!;
        public ICollection<NotificationRecipient> Recipients { get; set; } = new List<NotificationRecipient>();
        public ICollection<NotificationUserState> UserStates { get; set; } = new List<NotificationUserState>();
    }
}
