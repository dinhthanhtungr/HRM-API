using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.InteractionSummaries.Models.GenerateCustomerInteractionSummary
{
    public class CustomerInteractionAiSummaryBatchResult
    {
        public List<CustomerInteractionAiSummaryBatchResultItem> Items { get; set; } = new();
    }
}
