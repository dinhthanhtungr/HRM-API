using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.InteractionSummaries.Models.GenerateCustomerInteractionSummary
{
    public class CustomerInteractionAiSummaryResult
    {
        public string Summary { get; set; } = string.Empty;
        public string CustomerNeed { get; set; } = string.Empty;
        public string CurrentStage { get; set; } = string.Empty;
        public string NextAction { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string Sentiment { get; set; } = string.Empty;
    }
}
