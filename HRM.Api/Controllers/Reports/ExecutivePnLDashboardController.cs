using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
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
        private readonly ICurrentUser _currentUser;

        public ExecutivePnLDashboardController(ISender sender, ICurrentUser currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        [HttpGet("trend")]
        public async Task<IActionResult> GetTrend(
            [FromQuery] GetExecutivePnLDashboardTrendQuery query,
            CancellationToken cancellationToken)
        {
            if (!_currentUser.CompanyId.HasValue || _currentUser.CompanyId == Guid.Empty
                || (query.CompanyId.HasValue && query.CompanyId != _currentUser.CompanyId))
                return Forbid();
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-sales")]
        [Authorize(Roles = ApplicationRoleSets.Reports.SalesScopedReaderRolesCsv)]
        public async Task<IActionResult> GetBySales(
            [FromQuery] GetExecutivePnLDashboardBySalesQuery query,
            CancellationToken cancellationToken)
        {
            if (!_currentUser.CompanyId.HasValue || _currentUser.CompanyId == Guid.Empty
                || (query.CompanyId.HasValue && query.CompanyId != _currentUser.CompanyId))
                return Forbid();
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-product-type")]
        public async Task<IActionResult> GetByProductType(
            [FromQuery] GetExecutivePnLDashboardByProductTypeQuery query,
            CancellationToken cancellationToken)
        {
            if (!_currentUser.CompanyId.HasValue || _currentUser.CompanyId == Guid.Empty
                || (query.CompanyId.HasValue && query.CompanyId != _currentUser.CompanyId))
                return Forbid();
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-customer")]
        public async Task<IActionResult> GetByCustomer(
            [FromQuery] GetExecutivePnLDashboardByCustomerQuery query,
            CancellationToken cancellationToken)
        {
            if (!_currentUser.CompanyId.HasValue || _currentUser.CompanyId == Guid.Empty
                || (query.CompanyId.HasValue && query.CompanyId != _currentUser.CompanyId))
                return Forbid();
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}

