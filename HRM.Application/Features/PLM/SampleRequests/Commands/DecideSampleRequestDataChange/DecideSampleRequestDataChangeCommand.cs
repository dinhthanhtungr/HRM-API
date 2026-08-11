using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestDataChange;

/// <summary>
/// Lab duyệt hoặc từ chối một phần hay toàn bộ field còn chờ trong yêu cầu thay đổi.
/// </summary>
public sealed class DecideSampleRequestDataChangeCommand
    : IRequest<OperationResult<SampleRequestDataChangeDecisionResultDto>>
{
    public Guid SampleRequestId { get; set; }
    public Guid RequestMessageId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestDataChangeDecision Decision { get; set; }

    public IReadOnlyList<string> FieldCodes { get; set; } = Array.Empty<string>();
    public string? Reason { get; set; }
}
