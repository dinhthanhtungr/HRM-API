using HRM.Application.Features.PLM.SampleRequests.PersonalWork;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController, Authorize, Route("api/v1/plm/sample-requests")]
public sealed class SamplePersonalWorkController(ISender sender) : ControllerBase
{
    [HttpPut("customers/{customerId:guid}/personal-pins")]
    public async Task<IActionResult> SaveCustomerPins(Guid customerId, SaveSampleCustomerPinsRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SaveSampleCustomerPinsCommand(customerId, body), ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("personal-work/query")]
    public async Task<IActionResult> Query(SamplePersonalWorkBatchRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new GetSamplePersonalWorkQuery(body.SampleRequestIds), ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPut("{id:guid}/personal-work")]
    public async Task<IActionResult> Save(Guid id, SaveSamplePersonalWorkRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SaveSamplePersonalWorkCommand(id, body), ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
