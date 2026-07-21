using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VGroupDayCost
{
    public string? GroupCode { get; set; }

    public DateOnly? D { get; set; }

    public string? Band { get; set; }

    public decimal? Kwh { get; set; }

    public decimal? EnergyCostVnd { get; set; }
}
