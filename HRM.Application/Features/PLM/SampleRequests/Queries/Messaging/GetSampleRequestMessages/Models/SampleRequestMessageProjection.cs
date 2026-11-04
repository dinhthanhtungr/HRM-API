using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestMessages.Models
{
    internal sealed class SampleRequestMessageProjection
    {
        public Guid ConversationId { get; init; }

        public Guid MessageId { get; init; }

        public string Title { get; init; } = string.Empty;

        public NotificationSeverity Severity { get; init; }

        public string Message { get; init; } = string.Empty;

        public string? PayloadJson { get; init; }

        public Guid CreatedBy { get; init; }

        public string? CreatedByName { get; init; }

        public DateTime CreatedAt { get; init; }

        public bool IsRead { get; init; }

        public DateTime? ReadDate { get; init; }

        public string? MessageType { get; init; }

        public Guid? ReplyToMessageId { get; init; }
    }

}
