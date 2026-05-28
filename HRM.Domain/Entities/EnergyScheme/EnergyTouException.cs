using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    // Ngày giờ ngoại lệ của lịch TOU
    public class EnergyTouException
    {
        public int ExceptionId { get; set; }     // PK
        public int CalendarId { get; set; }     // FK -> energy.tou_calendar
        public DateOnly TheDate { get; set; }     // date
        public DateTime StartTime { get; set; }     // timestamptz (UTC)
        public DateTime EndTime { get; set; }     // timestamptz (UTC)
        public string? Band { get; set; }     // text (PEAK/OFF/MID...)
        public string? Note { get; set; }     // text

        public virtual EnergyTouCalendar Calendar { get; set; } = null!;
    }
}
