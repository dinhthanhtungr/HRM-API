using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class EquipmentDetailMRO
    {
        public int EquipmentId { get; set; }                    // PK + FK -> equipment
        public string? SerialNo { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public DateTime? PurchaseDate { get; set; }             // date
        public DateTime? CommissioningDate { get; set; }        // date
        public DateTime? WarrantyUntil { get; set; }            // date
        public string? Notes { get; set; }
        public DateTime? UpdatedAt { get; set; }                // timestamp
        public Guid? UpdatedBy { get; set; }
        public int? EquipmentTypeId { get; set; }               // FK -> equipmenttype

        public EquipmentMRO Equipment { get; set; } = null!;    // 1–1
        public EquipmentTypeMRO? EquipmentType { get; set; }    // n–1


    }
}

