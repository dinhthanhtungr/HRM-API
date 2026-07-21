using System;

namespace HRM.Infrastructure.DatabaseContext.Views.Energy;

public partial class VMetersRuntime
{
    public long? MeterId { get; set; }

    public string? MeterCode { get; set; }

    public string? MeterName { get; set; }

    public decimal? Multiplier { get; set; }

    public DateTime? LastAt { get; set; }

    public decimal? MinutesSinceLast { get; set; }

    public decimal? KwhLast1h { get; set; }

    public decimal? KwhToday { get; set; }

    public decimal? PowerKwEst { get; set; }

    public string? Status { get; set; }
}
