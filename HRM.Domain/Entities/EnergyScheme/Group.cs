using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class Group
{
    public short GroupId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public virtual ICollection<GroupTariffMap> GroupTariffMaps { get; set; } = new List<GroupTariffMap>();

    public virtual ICollection<MeterGroupHistory> MeterGroupHistories { get; set; } = new List<MeterGroupHistory>();

    public virtual ICollection<Meter> Meters { get; set; } = new List<Meter>();
}
