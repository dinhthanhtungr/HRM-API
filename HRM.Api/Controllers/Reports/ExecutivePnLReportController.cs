using HRM.Application.Abstractions.Security;
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
        private readonly ICurrentUser _currentUser;

        public ExecutivePnLReportController(ISender sender, ICurrentUser currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> GetExecutivePnLReport(
            [FromQuery] GetExecutivePnLReportQuery query,
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

