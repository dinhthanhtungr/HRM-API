using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Domain.Entities.Controllers.Reports
{
    [ApiController]
    [Authorize]
    [Route("api/v1/reports/executive/pnl")]
    public sealed class ExecutivePnLReportController : ControllerBase
    {
        private readonly ISender _sender;

        public ExecutivePnLReportController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> GetExecutivePnLReport(
            [FromQuery] GetExecutivePnLReportQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);

            return Ok(result);
        }
    }
}

