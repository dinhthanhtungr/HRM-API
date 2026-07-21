using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByCustomer;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByProductType;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardBySales;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardTrend;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Reports
{
    [ApiController]
    [Authorize]
    [Route("api/v1/reports/executive/pnl/dashboard")]
    public sealed class ExecutivePnLDashboardController : ControllerBase
    {
        private readonly ISender _sender;

        public ExecutivePnLDashboardController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("trend")]
        public async Task<IActionResult> GetTrend(
            [FromQuery] GetExecutivePnLDashboardTrendQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-sales")]
        public async Task<IActionResult> GetBySales(
            [FromQuery] GetExecutivePnLDashboardBySalesQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-product-type")]
        public async Task<IActionResult> GetByProductType(
            [FromQuery] GetExecutivePnLDashboardByProductTypeQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-customer")]
        public async Task<IActionResult> GetByCustomer(
            [FromQuery] GetExecutivePnLDashboardByCustomerQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}

