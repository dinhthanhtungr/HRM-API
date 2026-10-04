using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestFormulaOptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/sample-requests")]
public sealed class SampleRequestFormulaOptionsController : ControllerBase
{
    private readonly ISender _sender;

    public SampleRequestFormulaOptionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy header yêu cầu phối mẫu và tối đa năm công thức của từng nhóm.
    /// </summary>
    [HttpGet("{sampleRequestId:guid}/formula-options")]
    public async Task<IActionResult> GetFormulaOptions(
        Guid sampleRequestId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetSampleRequestFormulaOptionsQuery(sampleRequestId),
            cancellationToken);

        if (result is not null)
        {
            return Ok(result);
        }

        var accessStatus = await _sender.Send(new GetSampleRequestDetailAccessQuery
        {
            SampleRequestId = sampleRequestId
        }, cancellationToken);

        return accessStatus switch
        {
            SampleRequestDetailAccessStatus.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    code = "sample_request_forbidden",
                    message = "You do not have permission to read this sample request."
                }),
            SampleRequestDetailAccessStatus.InvalidRelationship => UnprocessableEntity(new
            {
                code = "sample_request_invalid_relationship",
                message = "Sample request is not linked to an active customer and product in the current company."
            }),
            _ => NotFound(new
            {
                code = "sample_request_not_found",
                message = "Sample request not found."
            })
        };
    }
}
