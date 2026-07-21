using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestMessages.Models
{
    internal sealed class SampleRequestMessagePayload
    {
        public Guid? ConversationId { get; init; }

        public Guid? MessageId { get; init; }

        public string? Type { get; init; }

        public string? SaleMessage { get; init; }

        public bool IsUrgent { get; init; }

    }
}
