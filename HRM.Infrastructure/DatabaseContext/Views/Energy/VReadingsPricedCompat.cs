using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VReadingsPricedCompat
{
    public string? GroupCode { get; set; }

    public DateTime? TsLocal { get; set; }

    public string? Band { get; set; }

    public decimal? KwhImport { get; set; }

    public decimal? EnergyCostVnd { get; set; }
}
