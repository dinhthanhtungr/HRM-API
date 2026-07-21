using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class TouWindow
{
    public int WindowId { get; set; }

    public int CalendarId { get; set; }

    public short Weekday { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Band { get; set; }

    public virtual TouCalendar Calendar { get; set; } = null!;
}
