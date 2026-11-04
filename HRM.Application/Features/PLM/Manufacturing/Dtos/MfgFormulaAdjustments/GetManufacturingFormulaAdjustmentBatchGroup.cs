using System.Collections.Generic;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class GetManufacturingFormulaAdjustmentBatchGroup
    {
        public int BatchFrom { get; set; }
        public int BatchTo { get; set; }
        public string DisplayRange => BatchFrom == BatchTo ? BatchFrom.ToString() : $"{BatchFrom} ... {BatchTo}";
        public string Signature { get; set; } = string.Empty;
        public GetManufacturingFormulaAdjustmentBatch BatchTemplate { get; set; } = default!;
        public List<GetManufacturingFormulaAdjustmentBatch> Batches { get; set; } = new();
    }
}
