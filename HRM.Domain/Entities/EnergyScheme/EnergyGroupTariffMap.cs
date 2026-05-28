using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyGroupTariffMap
    {
        public short GroupId { get; set; }   // FK -> energy.groups.group_id (smallint)
        public int TariffId { get; set; }   // FK -> energy.tariffs.tariff_id (int)
        public DateOnly ValidFrom { get; set; }   // date (PK part)
        public DateOnly? ValidTo { get; set; }   // date (NULL = vô hạn)

        public virtual EnergyGroup? Group { get; set; }
        public virtual EnergyTariff? Tariff { get; set; }
    }

}
