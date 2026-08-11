using System.Text.Json;

namespace HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;

public sealed class SampleRequestProposedChangeRequestDto
{
    public string FieldCode { get; set; } = string.Empty;
    public JsonElement NewValue { get; set; }
}

public sealed class SampleRequestDataChangeActionDto
{
    public string Type { get; set; } = SampleRequestDataChangePayloadTypes.Request;
    public string Status { get; set; } = SampleRequestDataChangeStatuses.Pending;
    public Guid SampleRequestId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public bool CanDecide { get; set; }
    public IReadOnlyList<SampleRequestDataChangeFieldDto> Changes { get; set; }
        = Array.Empty<SampleRequestDataChangeFieldDto>();
}

public sealed class SampleRequestDataChangeFieldDto
{
    public string FieldCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public JsonElement OldValue { get; set; }
    public JsonElement NewValue { get; set; }
    public string Status { get; set; } = SampleRequestDataChangeStatuses.Pending;
    public Guid? DecidedByEmployeeId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}

public sealed class SampleRequestDataChangeDecisionResultDto
{
    public Guid ConversationId { get; set; }
    public Guid RequestMessageId { get; set; }
    public Guid ResponseMessageId { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<string> ProcessedFieldCodes { get; set; } = Array.Empty<string>();
}
