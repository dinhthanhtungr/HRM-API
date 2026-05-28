using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class WarehouseVoucherDetail
    {
        public long VoucherDetailId { get; set; }   // PK (bigint identity)
        public long VoucherId { get; set; }         // FK -> WarehouseVoucher
        public int LineNo { get; set; }             // STT dòng
        public string ProductCode { get; set; } = default!;
        public string ProductName { get; set; } = default!;
        public string? LotNumber { get; set; }
        public decimal QtyKg { get; set; }          // (10,2)
        public int? Bags { get; set; }

        public int? SlotId { get; set; }            // FK -> WarehouseShelfStock.SlotId (int)
        public int? PurposeId { get; set; }         // FK -> UsagePurpose.PurposeId
        public bool IsIncrease { get; set; }        // tăng (true) / giảm (false)

        public DateTime ExpiryDate { get; set; }

        public VoucherDetailType VoucherType { get; set; } 
        public string? Note { get; set; }

        // Navigations
        public WarehouseVoucher Voucher { get; set; } = default!;
        public WarehouseShelfStock? Slot { get; set; }
        public UsagePurpose? Purpose { get; set; }

        public ICollection<QCInputByQC> QCInputByQCs { get; set; } = new List<QCInputByQC>();
    }
}
