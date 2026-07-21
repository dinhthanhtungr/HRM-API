using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VHourlySource
{
    public long? MeterId { get; set; }

    public DateTime? TsHourVn { get; set; }

    public decimal? KwhImport { get; set; }
}
