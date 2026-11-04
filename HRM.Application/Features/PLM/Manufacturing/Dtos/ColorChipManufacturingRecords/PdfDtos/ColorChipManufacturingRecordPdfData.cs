using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.PdfDtos
{
    public class ColorChipManufacturingRecordPdfData
    {
        public Guid ColorChipMfgRecordId { get; set; }
        public string? ColorCode { get; set; }
        public string? Name { get; set; }
        public ResinType ResinType { get; set; }
        public LogoType LogoType { get; set; }
        public FormStyle FormStyle { get; set; }
        public DateTime? RecordDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? Machine { get; set; }
        public string? StandardFormula { get; set; }
        public string? Resin { get; set; }
        public string? TemperatureLimit { get; set; }
        public string? SizeText { get; set; }
        public decimal? PelletWeightGram { get; set; }
        public string? NetWeightGram { get; set; }
        public bool? Electrostatic { get; set; }
        public string? Note { get; set; }
        public string? PrintNote { get; set; }
        public string? DeltaE { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductExternalId { get; set; }
        public string? ProductName { get; set; }
        public string? ColorName { get; set; }
        public string? FormulaExternalIdSnapshot { get; set; }
        public double? UsageRate { get; set; }
        public string? ManufacturingFormulaExternalId { get; set; }
        public string? ManufacturingFormulaName { get; set; }
        public string? PreparedByName { get; set; }
    }
}
