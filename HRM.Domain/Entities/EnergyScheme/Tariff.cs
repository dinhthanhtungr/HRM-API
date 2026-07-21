using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class Tariff
{
    public int TariffId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Currency { get; set; } = null!;

    public string? Utility { get; set; }

    public string? Note { get; set; }

    public virtual ICollection<GroupTariffMap> GroupTariffMaps { get; set; } = new List<GroupTariffMap>();

    public virtual ICollection<TariffVersion> TariffVersions { get; set; } = new List<TariffVersion>();
}
