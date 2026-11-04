using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class ManufacturingFormulaAdjustmentItem
    {
        public Guid ManufacturingFormulaAdjustmentItemId { get; set; }
        public Guid ManufacturingFormulaAdjustmentBatchId { get; set; }

        public Guid? MaterialId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid CategoryId { get; set; }


        public ItemType itemType { get; set; }
        public decimal? BaseQuantity { get; set; }       // khối lượng theo công thức gốc
        public decimal? AdjustedQuantity { get; set; }   // khối lượng QC muốn cân lại
        public decimal? DeltaQuantity { get; set; }      // chênh lệch: adjusted - base hoặc lượng thêm

        public string? Unit { get; set; }
        public string? Note { get; set; }

        public int LineNo { get; set; }
        public string LotNo { get; set; } = string.Empty;

        public string? MaterialNameSnapshot { get; set; }
        public string? MaterialExternalIdSnapshot { get; set; }
        public string? ProductNameSnapshot { get; set; }
        public string? ProductExternalIdSnapshot { get; set; }

        public virtual ManufacturingFormulaAdjustmentBatch ManufacturingFormulaAdjustmentBatch { get; set; } = default!;
        public virtual Material? Material { get; set; }
        public virtual Product? Product { get; set; }
        public virtual Category Category { get; set; } = default!;
    }
}
