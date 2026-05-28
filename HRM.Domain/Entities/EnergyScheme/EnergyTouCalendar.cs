using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    // Lịch TOU (time-of-use)
    public class EnergyTouCalendar
    {
        public int CalendarId { get; set; }     // PK
        public string Code { get; set; } = ""; // unique
        public string? Tz { get; set; }       // múi giờ (VD: "Asia/Ho_Chi_Minh")
        public DateOnly? StartDate { get; set; }       // date
        public DateOnly? EndDate { get; set; }       // date

        public virtual ICollection<EnergyTouException> Exceptions { get; set; } = new List<EnergyTouException>();
    }
}
