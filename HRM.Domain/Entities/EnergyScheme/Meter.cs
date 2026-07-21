using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.EnergyScheme;

public partial class Meter
{
    public long MeterId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public decimal Multiplier { get; set; }

    public bool IsActive { get; set; }

    public short GroupId { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual ICollection<MeterGroupHistory> MeterGroupHistories { get; set; } = new List<MeterGroupHistory>();

    public virtual ICollection<ReadingsHourly> ReadingsHourlies { get; set; } = new List<ReadingsHourly>();

    public virtual ICollection<ReadingsHourlyVn> ReadingsHourlyVns { get; set; } = new List<ReadingsHourlyVn>();

    public virtual ICollection<RegisterSnapshot> RegisterSnapshots { get; set; } = new List<RegisterSnapshot>();
}
