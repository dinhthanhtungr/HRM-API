using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions
{
    public class SampleRequestOptionDto
    {
        public Guid Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
