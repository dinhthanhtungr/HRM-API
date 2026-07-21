using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VGroupMonthBill
{
    public string? GroupCode { get; set; }

    public DateOnly? Ym { get; set; }

    public decimal? Kwh { get; set; }

    public decimal? EnergyCostVnd { get; set; }

    public decimal? FixedFeeVnd { get; set; }

    public decimal? SubtotalVnd { get; set; }

    public decimal? VatVnd { get; set; }

    public decimal? TotalVnd { get; set; }
}
