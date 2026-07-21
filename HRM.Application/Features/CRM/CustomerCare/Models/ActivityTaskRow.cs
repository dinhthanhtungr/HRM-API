using HRM.Domain.Enums.WorkTaskEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.CustomerCare.Models
{
    internal sealed class ActivityTaskRow
    {
        public Guid CustomerId { get; set; }
        public WorkTaskStatus Status { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime ActivityDate { get; set; }
    }
}
