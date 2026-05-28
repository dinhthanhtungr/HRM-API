using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyTouWindow
    {
        public int WindowId { get; set; }      // PK (serial)
        public int CalendarId { get; set; }      // FK -> energy.tou_calendar
        public short Weekday { get; set; }      // 0=Sun..6=Sat
        public DateTime StartTime { get; set; }      // UTC (timestamptz)
        public DateTime EndTime { get; set; }      // UTC (timestamptz)
        public string? Band { get; set; }      // PEAK/OFF/MID...

        // optional nav
        public virtual EnergyTouCalendar? Calendar { get; set; }
    }
}
