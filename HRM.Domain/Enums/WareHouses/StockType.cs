using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.WareHouses
{
    public enum StockType
    {
        FinishedGood = 1,           // Hàng thành phẩm
        DefectiveFinishedGood = 2,  // Hàng thành phẩm lỗi
        RawMaterial = 3,            // Nguyên vật liệu
        DefectiveRawMaterial = 4,   // Nguyên vật liệu lỗi
        Other = 5,                  // Khác
        Waiter = 6,                 // Chưa QC; giá trị tồn tại trong hệ legacy
        Material = 7                // Vật tư/bao bì; giá trị tồn tại trong hệ legacy
    }
}
