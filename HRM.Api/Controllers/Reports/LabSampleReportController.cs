using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Reports.Lab.Samples.Queries.GetLabSampleReport;
using HRM.Application.Features.Reports.Lab.Samples.Queries.GetLabSampleReportItems;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Reports;

[ApiController]
[Authorize]
[Route("api/v1/reports/lab/samples")]
public sealed class LabSampleReportController(ISender sender, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetLabSampleReportQuery query, CancellationToken ct)
    {
        if (!user.CompanyId.HasValue || user.CompanyId == Guid.Empty || !user.EmployeeId.HasValue
            || user.EmployeeId == Guid.Empty || query.CompanyId.HasValue && query.CompanyId != user.CompanyId) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems([FromQuery] GetLabSampleReportItemsQuery query, CancellationToken ct)
    {
        if (!user.CompanyId.HasValue || user.CompanyId == Guid.Empty || !user.EmployeeId.HasValue
            || user.EmployeeId == Guid.Empty || query.CompanyId.HasValue && query.CompanyId != user.CompanyId) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
