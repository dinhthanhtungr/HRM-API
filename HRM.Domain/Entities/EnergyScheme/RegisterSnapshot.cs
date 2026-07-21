using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class RegisterSnapshot
{
    public long MeterId { get; set; }

    public DateTime TsUtc { get; set; }

    public decimal KwhTotal { get; set; }

    public string? Source { get; set; }

    public virtual Meter Meter { get; set; } = null!;
}
