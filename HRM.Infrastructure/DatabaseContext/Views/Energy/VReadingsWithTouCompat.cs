using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VReadingsWithTouCompat
{
    public long? MeterId { get; set; }

    public DateTime? TsHourVn { get; set; }

    public decimal? KwhImport { get; set; }

    public int? GroupId { get; set; }

    public string? GroupCode { get; set; }

    public string? Band { get; set; }
}
