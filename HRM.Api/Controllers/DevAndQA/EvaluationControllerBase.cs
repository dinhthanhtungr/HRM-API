using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

public abstract class EvaluationControllerBase(ISender sender) : ControllerBase
{
    protected async Task<IActionResult> Execute<T>(IRequest<T> request, CancellationToken ct, bool created = false)
    {
        try
        {
            var data = await sender.Send(request, ct);
            return StatusCode(created ? 201 : 200, OperationResult<T>.Ok(data));
        }
        catch (EvaluationRequestException ex)
        {
            return StatusCode(ex.StatusCode, OperationResult.Fail(ex.Message));
        }
    }
}
