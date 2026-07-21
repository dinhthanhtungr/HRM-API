using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VReadingsPriced
{
    public long? MeterId { get; set; }

    public DateTime? TsUtc { get; set; }

    public DateTime? TsLocal { get; set; }

    public int? GroupId { get; set; }

    public string? GroupCode { get; set; }

    public string? Band { get; set; }

    public int? VersionId { get; set; }

    public decimal? KwhImport { get; set; }

    public decimal? PriceVndPerKwh { get; set; }

    public decimal? EnergyCostVnd { get; set; }

    public decimal? VatRate { get; set; }

    public decimal? FuelAdjVndPerKwh { get; set; }

    public string? Quality { get; set; }
}
