using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities;

namespace HRM.Domain.Enums.WareHouses
{
    public enum TempType { Snapshot = 1, Reserve = 2 }
    public enum ReserveStatus { Open = 1, Consumed = 2, Cancelled = 3 }
}


/*

    TempType(dùng trên bảng WarehouseTempStock)

        Phân loại một dòng trong bảng tạm:

            Snapshot(= 1):  Dòng ảnh chụp tồn (để tham chiếu/audit).

                            Dòng này không có trạng thái đặt chỗ.

            Reserve (=2):   Dòng đặt chỗ khi người dùng bấm OK.

                            Dòng này có trạng thái (ReserveStatus).


    ReserveStatus (chỉ áp dụng khi TempType = Reserve)

        Trạng thái vòng đời của một đặt chỗ:

            Open(= 1):      Đang giữ chỗ (chưa xuất kho thật).

            Consumed (=2):  Đã xuất kho thật (đặt chỗ được “tiêu thụ”).

            Cancelled (=3): Huỷ giữ chỗ.

*/
