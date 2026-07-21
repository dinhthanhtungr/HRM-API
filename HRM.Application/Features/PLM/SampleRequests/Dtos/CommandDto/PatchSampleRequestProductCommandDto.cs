namespace HRM.Application.Features.PLM.SampleRequests.Dtos.CommandDto;

public sealed class PatchSampleRequestProductCommandDto
{
    public string? ColourCode { get; set; }
    public string? Name { get; set; }
    public string? ColourName { get; set; }
    public string? Additive { get; set; }
    public double? UsageRate { get; set; }
    public string? DeltaE { get; set; }
    public string? Requirement { get; set; }
    public string? ExpiryType { get; set; }
    public bool? StorageCondition { get; set; }
    public string? LabComment { get; set; }
    public string? Procedure { get; set; }
    public double? RecycleRate { get; set; }
    public double? TaicalRate { get; set; }
    public string? Application { get; set; }
    public string? ProductUsage { get; set; }
    public string? PolymerMatchedIn { get; set; }
    public string? Code { get; set; }
    public string? EndUser { get; set; }
    public bool? FoodSafety { get; set; }
    public bool? RohsStandard { get; set; }
    public bool? ReachStandard { get; set; }
    public double? MaxTemp { get; set; }
    public string? WeatherResistance { get; set; }
    public string? LightCondition { get; set; }
    public string? VisualTest { get; set; }
    public bool? ReturnSample { get; set; }
    public bool? IsRecycle { get; set; }
    public string? OtherComment { get; set; }
    public Guid? CategoryId { get; set; }
    public double? Weight { get; set; }
    public string? Unit { get; set; }
}
