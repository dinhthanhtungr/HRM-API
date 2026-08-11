using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestFormulaChange;

public sealed class DecideSampleRequestFormulaChangeCommand
    : IRequest<OperationResult<SampleRequestFormulaChangeDecisionResultDto>>
{
    public Guid SampleRequestId { get; set; }
    public Guid RequestMessageId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestFormulaChangeDecision Decision { get; set; }

    public string? Reason { get; set; }
}
