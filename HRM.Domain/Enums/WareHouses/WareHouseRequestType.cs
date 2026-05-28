using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.WareHouses
{
    /// <summary>
    /// StockType sẽ được sử dụng để phân loại loại hàng hóa trong kho, giúp quản lý và báo cáo chính xác hơn. Ví dụ:
    /// - FinishedGood: Hàng thành phẩm
    /// - DefectiveFinishedGood: Hàng thành phẩm lỗi
    /// - RawMaterial: Nguyên vật liệu
    /// - DefectiveRawMaterial: Nguyên vật liệu lỗi
    /// - Other: Khác
    /// Đây là stocktype bên anh Qúy mình để vậy cho nhớ
    /// </summary>
    public enum WareHouseRequestType
    {
        ImportFinishedGood = 1,           // Nhập hàng thành phẩm
        ImportDefectiveFinishedGood = 3,  // Nhập hàng thành phẩm lỗi
        ImportRawMaterial = 5,            // Nhập nguyên vật liệu
        ImportDefectiveRawMaterial = 7,   // Nhập nguyên vật liệu lỗi
        ImportOther = 9,                  // Nhập khác
        ImportReturn = 11,                // Nhập trả hàng 
        ImportMaterial = 13,              // Nhập vật tư

        ExportFinishedGood = 2,           // Xuất hàng thành phẩm
        ExportDefectiveFinishedGood = 4,  // Xuất hàng thành phẩm lỗi
        ExportRawMaterial = 6,            // Xuất nguyên vật liệu
        ExportDefectiveRawMaterial = 8,   // Xuất nguyên vật liệu lỗi
        ExportOther = 10,                 // Xuất khác
        ExportReturn = 12,               // Xuất trả hàng
        ExportMaterial = 14,             // Xuất vật tư
    }
}
