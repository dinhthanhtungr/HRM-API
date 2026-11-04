namespace HRM.Application.Features.PLM.Materials.Dtos;

/// <summary>Lead time từ ngày tạo PO đến ngày giao thực tế đầu tiên theo IQC.</summary>
public sealed class SupplierActualDeliveryDto
{
    public double? AverageDays { get; set; }
    public int SampleCount { get; init; }
    public int MissingDateCount { get; init; }
    public int InvalidDateCount { get; init; }
    public int OrderCount { get; init; }
}
