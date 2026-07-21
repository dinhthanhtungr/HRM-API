using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.InteractionSummaries.Models.GenerateCustomerInteractionSummary
{
    public class CustomerInteractionAiSummaryBatchResultItem : CustomerInteractionAiSummaryResult
    {
        public Guid CustomerId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
