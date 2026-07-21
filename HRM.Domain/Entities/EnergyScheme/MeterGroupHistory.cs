using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class MeterGroupHistory
{
    public long MeterId { get; set; }

    public DateTime ValidFrom { get; set; }

    public short GroupId { get; set; }

    public DateTime? ValidTo { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual Meter Meter { get; set; } = null!;
}
