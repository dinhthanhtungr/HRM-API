using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class TariffBandRate
{
    public int VersionId { get; set; }

    public string Band { get; set; } = null!;

    public decimal PriceVndPerKwh { get; set; }

    public virtual TariffVersion Version { get; set; } = null!;
}
