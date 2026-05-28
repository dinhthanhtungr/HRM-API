using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.PrintectSchema
{
    public class LabelTemplate
    {
        public int TemplateId { get; set; }
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public string? LabelType { get; set; }
    }
}
