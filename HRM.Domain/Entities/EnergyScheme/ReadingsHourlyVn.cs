using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class ReadingsHourlyVn
{
    public long MeterId { get; set; }

    public DateTime TsHourVn { get; set; }

    public decimal KwhImport { get; set; }

    public string? Quality { get; set; }

    public string? Source { get; set; }

    public virtual Meter Meter { get; set; } = null!;
}
