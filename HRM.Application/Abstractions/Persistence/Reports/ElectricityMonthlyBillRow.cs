using System;

namespace HRM.Application.Abstractions.Persistence.Reports;

public sealed class ElectricityMonthlyBillRow
{
    public string GroupCode { get; set; } = string.Empty;
    public DateTime Ym { get; set; }
    public decimal Kwh { get; set; }
    public decimal EnergyCostVnd { get; set; }
    public decimal FixedFeeVnd { get; set; }
    public decimal SubtotalVnd { get; set; }
    public decimal VatVnd { get; set; }
    public decimal TotalVnd { get; set; }
}
