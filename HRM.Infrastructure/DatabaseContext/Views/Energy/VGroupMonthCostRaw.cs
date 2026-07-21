using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VGroupMonthCostRaw
{
    public string? GroupCode { get; set; }

    public DateOnly? Ym { get; set; }

    public decimal? Kwh { get; set; }

    public decimal? EnergyCostVnd { get; set; }

    public decimal? VatRateAny { get; set; }

    public decimal? FuelAdjAny { get; set; }
}
