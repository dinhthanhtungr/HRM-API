using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos
{
    public class GetColorChipManufacturingRecord
    {
        public Guid ColorChipMfgRecordId { get; set; }
        public string? ColorCode { get; set; }
        public string? ProductName { get; set; }
        public ResinType ResinType { get; set; }
        public LogoType LogoType { get; set; }
        public FormStyle FormStyle { get; set; }
        public Guid? MfgProductionOrderId { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public Guid? ManufacturingFormulaId { get; set; }
        public string? ManufacturingFormulaExternalId { get; set; }
        public string? ManufacturingFormulaName { get; set; }
        public string? StandardFormula { get; set; }
        public string? Machine { get; set; }
        public string? Resin { get; set; }
        public string? TemperatureLimit { get; set; }
        public string? SizeText { get; set; }
        public decimal? PelletWeightGram { get; set; }
        public string? NetWeightGram { get; set; }
        public bool? Electrostatic { get; set; }
        public string? DeltaE { get; set; }
        public double? UsageRate { get; set; }
        public DateTime? RecordDate { get; set; }
        public string? Note { get; set; }
        public string? PrintNote { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Guid CompanyId { get; set; }
        public bool IsActive { get; set; }
    }
}
