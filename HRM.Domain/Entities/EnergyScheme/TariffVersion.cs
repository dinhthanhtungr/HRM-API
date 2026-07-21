using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class TariffVersion
{
    public int VersionId { get; set; }

    public int TariffId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public decimal VatRate { get; set; }

    public decimal FuelAdjVndPerKwh { get; set; }

    public decimal ServiceFixedVndPerMonth { get; set; }

    public decimal DemandRateVndPerKw { get; set; }

    public virtual Tariff Tariff { get; set; } = null!;

    public virtual ICollection<TariffBandRate> TariffBandRates { get; set; } = new List<TariffBandRate>();
}
