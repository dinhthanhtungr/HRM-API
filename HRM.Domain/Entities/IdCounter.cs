using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities
{
    public class IdCounter
    {
        public Guid CompanyId { get; set; }
        public string Prefix { get; set; } = default!;
        public string Period { get; set; } = default!; // "ddMMyy"
        public int LastNo { get; set; }
    }
}
