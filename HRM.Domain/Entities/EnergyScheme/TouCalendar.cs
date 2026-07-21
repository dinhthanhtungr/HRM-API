using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class TouCalendar
{
    public int CalendarId { get; set; }

    public string Code { get; set; } = null!;

    public string? Tz { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public virtual ICollection<TouException> TouExceptions { get; set; } = new List<TouException>();

    public virtual ICollection<TouWindow> TouWindows { get; set; } = new List<TouWindow>();
}
