using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.CustomerCare.Models
{
    internal sealed class ActivityInteractionRow
    {
        public Guid CustomerId { get; set; }
        public DateTime InteractionAt { get; set; }
    }
}
