using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VMeterGroupActiveHourly
{
    public short? GroupId { get; set; }

    public string? GroupCode { get; set; }

    public long? MeterId { get; set; }

    public DateTime? TsHourVn { get; set; }
}
