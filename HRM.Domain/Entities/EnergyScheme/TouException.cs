using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class TouException
{
    public int ExceptionId { get; set; }

    public int CalendarId { get; set; }

    public DateOnly TheDate { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Band { get; set; }

    public string? Note { get; set; }

    public virtual TouCalendar Calendar { get; set; } = null!;
}
