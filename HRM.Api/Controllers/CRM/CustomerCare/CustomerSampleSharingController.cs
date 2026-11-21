using HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

[ApiController, Authorize, Route("api/v1/crm/customer-sample-sharing")]
public sealed class CustomerSampleSharingController(ISender sender) : ControllerBase
{
    [HttpPut("groups/members")]
    public async Task<IActionResult> SaveMembers(SaveSampleSharingMembersRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveSampleSharingMembersCommand(request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("groups")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.Send(new GetCustomerSampleSharingQuery(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("customers/{customerId:guid}/samples")]
    public async Task<IActionResult> Samples(Guid customerId, [FromQuery] string? keyword, CancellationToken ct)
    {
        var result = await sender.Send(new GetCustomerSharedSamplesQuery(customerId, keyword), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost("groups")]
    public async Task<IActionResult> Create(CreateSampleSharingGroupRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateCustomerSampleSharingGroupCommand(request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPut("customers/{customerId:guid}")]
    public async Task<IActionResult> Assign(Guid customerId, AssignSampleSharingGroupRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AssignCustomerSampleSharingGroupCommand(customerId, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("customers/{customerId:guid}/link")]
    public async Task<IActionResult> Link(Guid customerId, LinkSampleSharingCustomersRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new LinkCustomerSamplesCommand(customerId, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
