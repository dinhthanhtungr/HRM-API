using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.AuditSchema
{
    public class CodeCounter
    {
        public string Prefix { get; set; } = default!;
        public int Ymd { get; set; }     
        public int LastValue { get; set; }
    }
}
