using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class WarehouseRequestDetail
    {
        public int DetailId { get; set; }              // PK (DetailID)

        public int RequestId { get; set; }      // liên kết header qua RequestCode
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? LotNumber { get; set; } = string.Empty;
        public decimal WeightKg { get; set; } = 0;
        public int BagNumber { get; set; } = 0;
        public string? StockStatus { get; set; } = string.Empty;
        public StockType? ItemStockType { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;
        // Navigation properties
        public WarehouseRequest? WarehouseRequest { get; set; }

        public ICollection<QCInputByQC> QCInputByQCs { get; set; } = new List<QCInputByQC>();
    }
}
