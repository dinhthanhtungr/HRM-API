using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs
{
    public class GetMfgProductionOrderRWSummary
    {
        public Guid? MfgProductionOrderId { get; set; }
        public FormulaType? FormulaType { get; set; }
        public string? ExternalId { get; set; }
        public string? Note { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public Guid Id { get; set; }
    }

    public enum FormulaType
    {
        FromVu = 1, // Công thức VU
        Standard = 2, // Công thức chuẩn
        ProductionOld = 3, // Công thức sản xuất cũ (trước 01/03/2026)
        Production = 4, // Công thức sản xuất mới
        Improvement = 5, // Công thức cải tiến
    }
}
