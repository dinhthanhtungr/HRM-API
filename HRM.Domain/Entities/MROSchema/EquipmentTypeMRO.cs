using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class EquipmentTypeMRO
    {
        public int EquipmentTypeId { get; set; }
        public string EquipmentTypeName { get; set; } = string.Empty;

        public ICollection<EquipmentDetailMRO> EquipmentDetails { get; set; } = new List<EquipmentDetailMRO>();
    }
}
