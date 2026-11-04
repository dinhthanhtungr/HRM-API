using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos
{
    public class GetColorChipManufacturingRecordSummary
    {
        public Guid ColorChipMfgRecordId { get; set; }
        public string? ColorCode { get; set; }
        public string? ProductName { get; set; }
        public Guid? MfgProductionOrderId { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public Guid? ManufacturingFormulaId { get; set; }
        public string? ManufacturingFormulaExternalId { get; set; }
        public ResinType ResinType { get; set; }
        public LogoType LogoType { get; set; }
        public FormStyle FormStyle { get; set; }
        public DateTime? RecordDate { get; set; }
        public string? Machine { get; set; }
        public double? UsageRate { get; set; }
        public bool IsActive { get; set; }
    }
}
