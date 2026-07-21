using HRM.Application.Features.CRM.InteractionSummaries.Models.GenerateCustomerInteractionSummary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Abstractions.Commons.Ais.CRM
{
    public interface ICustomerInteractionAiSummaryClient
    {
        string Model { get; }

        Task<CustomerInteractionAiSummaryResult> GenerateSummaryAsync(
            string prompt,
            CancellationToken cancellationToken = default);

        Task<CustomerInteractionAiSummaryBatchResult> GenerateBatchSummaryAsync(
            string prompt,
            CancellationToken cancellationToken = default);
    }
}
