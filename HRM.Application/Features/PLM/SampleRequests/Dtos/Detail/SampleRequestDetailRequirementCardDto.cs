namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailRequirementCardDto
    {
        public string RequestType { get; set; } = string.Empty;
        public double? ExpectedQuantity { get; set; }
        public decimal? ExpectedPrice { get; set; }
        public double? SampleQuantity { get; set; }
        public int? NumberDeliverySampleDate { get; set; }
        public string Package { get; set; } = string.Empty;
        public int BagWeight { get; set; }
        public string? CustomerProductCode { get; set; }
        public DateTime? RequestDeliveryDate { get; set; }
        public DateTime? LabReceivedDate { get; set; }
        public bool IsDelayed { get; set; }
        public Guid? LabReceivedByEmployeeId { get; set; }
        public string? LabReceivedByName { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? RealDeliveryDate { get; set; }
        public DateTime? RequestTestSampleDate { get; set; }
        public DateTime? ResponseDeliveryDate { get; set; }
        public DateTime? ExpectedPriceQuoteDate { get; set; }
        public DateTime? RealPriceQuoteDate { get; set; }
    }
}
