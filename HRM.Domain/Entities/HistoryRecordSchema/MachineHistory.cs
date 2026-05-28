using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HistoryRecordSchema
{
    public class MachineHistory
    {
        public long Id { get; set; }                          // MachineHistory_Id
        public DateTime CreatedAt { get; set; } = DateTime.Now;               // timestamp

        // thời lượng (phút/giây tùy bạn, hiện để int)
        public int ProducingTimeOfDay { get; set; }           // thời gian SX trong ngày
        public int WaitingTimeOfDay { get; set; }             // thời gian chờ
        public int MachineCleansingTimeOfDay { get; set; }    // thời gian rửa máy

        // năng lượng (đơn vị kWh/Wh tùy dự án)
        public int EnergyTotalOfDay { get; set; }
        public int ProducingEnergyOfDay { get; set; }
        public int MachineCleansingEnergyOfDay { get; set; }
        public int WaitingEnergyOfDay { get; set; }
    }
}
