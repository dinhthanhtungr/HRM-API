using HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardDrilldown;
using HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardMonthlySummary;
using HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardPivotHub;
using HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardSummary;
using HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardTaskBreakdown;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Domain.Entities.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] GetDashboardSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("monthly-summary")]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] GetDashboardMonthlySummaryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("pivot-hub")]
    public async Task<IActionResult> GetPivotHub(
        [FromQuery] GetDashboardPivotHubQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("drilldown")]
    public async Task<IActionResult> GetDrilldown(
        [FromQuery] GetDashboardDrilldownQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("task-breakdown")]
    public async Task<IActionResult> GetTaskBreakdown(
        [FromQuery] GetDashboardTaskBreakdownQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }
}
