using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VMissingLast24h
{
    public string? MeterCode { get; set; }

    public DateTime? HourUtcExpected { get; set; }
}
