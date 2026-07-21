using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VReadingsWithTou
{
    public long? MeterId { get; set; }

    public DateTime? TsUtc { get; set; }

    public decimal? KwhImport { get; set; }

    public string? Quality { get; set; }

    public string? Source { get; set; }

    public int? CalendarId { get; set; }

    public string? CalCode { get; set; }

    public string? Tz { get; set; }

    public DateTime? TsLocal { get; set; }

    public string? Band { get; set; }
}
