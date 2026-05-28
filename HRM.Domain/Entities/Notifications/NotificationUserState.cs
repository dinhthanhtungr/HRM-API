using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.Notifications
{
    public class NotificationUserState
    {
        public Guid NotificationId { get; set; }
        public Notification Notification { get; set; } = default!;

        public Guid UserId { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime? ReadDate { get; set; }
        public bool IsArchived { get; set; } = false;
    
        public Employee User { get; set; } = default!;
    }
}
