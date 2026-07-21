using HRM.Application.Commons.Models;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequest;

public sealed class CreateSampleRequestCommand : IRequest<OperationResult<Guid>>
{
    public Guid CustomerId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid? FormulaId { get; set; }
    public string Status { get; set; } = SampleRequestStatus.New.ToString();
    public string RequestType { get; set; } = string.Empty;
    public double? ExpectedQuantity { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public double? SampleQuantity { get; set; }
    public int? NumberDeliverySampleDate { get; set; }
    public string Package { get; set; } = string.Empty;
    public int BagWeight { get; set; }
    public string? CustomerProductCode { get; set; }

    public DateTime? RequestDeliveryDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? RequestTestSampleDate { get; set; }
    public DateTime? ExpectedPriceQuoteDate { get; set; }

    public string? InfoType { get; set; }
    public string? OtherComment { get; set; }
    public string? SaleComment { get; set; }
    public string? AdditionalComment { get; set; }

    public string? ColourCode { get; set; }
    public string? ProductName { get; set; }
    public string? ColourName { get; set; }
    public string? Additive { get; set; }
    public double? UsageRate { get; set; }
    public string? DeltaE { get; set; }
    public string? ProductRequirement { get; set; }
    public string? ExpiryType { get; set; }
    public bool? StorageCondition { get; set; }
    public string? Application { get; set; }
    public string? ProductUsage { get; set; }
    public string? PolymerMatchedIn { get; set; }
    public string? ProductCode { get; set; }
    public string? EndUser { get; set; }
    public bool? FoodSafety { get; set; }
    public bool? RohsStandard { get; set; }
    public bool? ReachStandard { get; set; }
    public double? MaxTemp { get; set; }
    public string? WeatherResistance { get; set; }
    public string? LightCondition { get; set; }
    public string? VisualTest { get; set; }
    public bool? ReturnSample { get; set; }
    public string? LabComment { get; set; }
    public string? Procedure { get; set; }
    public double? RecycleRate { get; set; }
    public double? TaicalRate { get; set; }
    public bool? IsRecycle { get; set; }
    public Guid? CategoryId { get; set; }
    public double? Weight { get; set; }
    public string? Unit { get; set; }
    public string? ProductOtherComment { get; set; }
}
