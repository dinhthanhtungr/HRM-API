using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

public sealed record SaveSampleSharingMembersRequest(
    [property: JsonRequired] Guid? GroupId,
    [property: JsonRequired] Guid? AnchorCustomerId,
    [property: JsonRequired] string Revision,
    string? Name,
    [property: JsonRequired] IReadOnlyList<Guid> CustomerIds);

public sealed record SaveSampleSharingMembersCommand(SaveSampleSharingMembersRequest Request) : IRequest<OperationResult<Guid>>;
