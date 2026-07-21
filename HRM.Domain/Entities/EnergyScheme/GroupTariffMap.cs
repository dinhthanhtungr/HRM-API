using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class GroupTariffMap
{
    public short GroupId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public int TariffId { get; set; }

    public DateOnly? ValidTo { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual Tariff Tariff { get; set; } = null!;
}
