using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailTechnicalRequirementCardDto
    {
        public string? InfoType { get; set; }
        public string? SaleComment { get; set; } // Lưu ý
        public string? AdditionalComment { get; set; } // Yêu cầu đặt biệt
        public string? Requirement { get; set; } // Yêu cầu sản xuất và QC thực hiện
        public string? Additive { get; set; }
        public string? AdditiveLabel { get; set; }
        public double? UsageRate { get; set; }
        public string? DeltaE { get; set; }
        public string? ExpiryType { get; set; }
        public bool? StorageCondition { get; set; }
        public string? LabComment { get; set; } // Lab ghi chú
        public string? Procedure { get; set; }
        public double? RecycleRate { get; set; }
        public double? TaicalRate { get; set; }
        public string? Application { get; set; }
        public string? ProductUsage { get; set; }
        public string? PolymerMatchedIn { get; set; }
        public string? EndUser { get; set; }
        public bool? FoodSafety { get; set; }
        public bool? RohsStandard { get; set; }
        public bool? ReachStandard { get; set; }
        public double? MaxTemp { get; set; }
        public string? WeatherResistance { get; set; }
        public string? LightCondition { get; set; }
        public string? VisualTest { get; set; }
        public bool? ReturnSample { get; set; }
        public bool IsRecycle { get; set; }
        public bool GRS { get; set; }
        public GRSConsumerType? GRSConsumerType { get; set; }
        public string? OtherComment { get; set; } // Yêu cầu khác
    }
}
