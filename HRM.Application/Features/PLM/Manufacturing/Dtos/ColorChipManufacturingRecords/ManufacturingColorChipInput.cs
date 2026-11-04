using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords;

/// <summary>Full form kỹ thuật. Mã/tên nhập tay tùy chọn; null khi create lấy mặc định từ MFG.</summary>
public sealed class ManufacturingColorChipInput
{
    [MaxLength(255)]
    public string? ColorCode { get; init; }

    [MaxLength(255)]
    public string? ProductName { get; init; }
    public ResinType? ResinType { get; init; }
    public LogoType? LogoType { get; init; }
    public FormStyle? FormStyle { get; init; }

    [JsonRequired]
    public Guid? MfgProductionOrderId { get; init; }

    [JsonRequired]
    public Guid? ManufacturingFormulaId { get; init; }

    [MaxLength(255)]
    public string? StandardFormula { get; init; }

    [MaxLength(255)]
    public string? Machine { get; init; }

    [MaxLength(255)]
    public string? Resin { get; init; }

    [MaxLength(255)]
    public string? TemperatureLimit { get; init; }

    [MaxLength(255)]
    public string? SizeText { get; init; }

    [JsonRequired]
    public decimal? PelletWeightGram { get; init; }

    [MaxLength(255)]
    public string? NetWeightGram { get; init; }

    [JsonRequired]
    public bool? Electrostatic { get; init; }

    [MaxLength(255)]
    public string? DeltaE { get; init; }

    [JsonRequired]
    public double? UsageRate { get; init; }

    [JsonRequired]
    public DateTime? RecordDate { get; init; }

    [MaxLength(2000)]
    public string? Note { get; init; }

    [MaxLength(2000)]
    public string? PrintNote { get; init; }
}
