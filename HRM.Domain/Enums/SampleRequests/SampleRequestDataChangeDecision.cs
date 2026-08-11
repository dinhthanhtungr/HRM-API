using System.Text.Json.Serialization;

namespace HRM.Domain.Enums.SampleRequests;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SampleRequestDataChangeDecision
{
    Approve = 0,
    Reject = 1
}
