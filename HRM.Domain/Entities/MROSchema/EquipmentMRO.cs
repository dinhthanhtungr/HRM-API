using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class EquipmentMRO
    {
        public int EquipmentId { get; set; }                  // PK
        public string EquipmentExternalId { get; set; } = default!;
        public string EquipmentName { get; set; } = default!;

        // Tham chiếu mềm bằng external id (KHÔNG FK)

        public int AreaId { get; set; }                     // tham chiếu 
        public string AreaExternalId { get; set; } = default!;  // tham chiếu mềm

        public Guid FactoryId { get; set; }
        public string FactoryExternalId { get; set; } = default!;

        public Guid PartId { get; set; }                     // tham chiếu
        public string? PartExternalId { get; set; }           // citext

        //public Guid EnergygroupId { get; set; }              // tham chiếu
        //public string? EnergygroupExternalId { get; set; }

        // Liên kết cứng có thể có hoặc không
        public Company? Factory { get; set; }                 // nếu có entity Company
        public Part? Part { get; set; }                       // nếu có entity Part
        public AreaMRO? Area { get; set; }                       // nếu có entity
                                                                 // Area
        public ICollection<EquipmentDetailMRO> EquipmentDetails { get; set; } = new List<EquipmentDetailMRO>();
        //public virtual ICollection<IncidentHeaderMRO> IncidentHeaderMROs { get; set; } = new List<IncidentHeaderMRO>();
    }
}
