using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.CompanyOverview;
using HRM.Application.Features.Reports.CompanyOverview.ExportCompanyOverview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Reports;

[ApiController]
[Authorize(Policy = HRM.Api.Security.Authorization.SensitiveDataAuthorization.PolicyName)]
[Route("api/v1/reports/company-overview")]
public sealed class CompanyOverviewReportController(
    ISender sender, ICurrentUser user, ICurrentUserPermissionService permissions) : ControllerBase
{
    [HttpGet("export-excel")]
    public async Task<IActionResult> Export([FromQuery] ExportCompanyOverviewQuery query, CancellationToken ct)
    {
        if (!CompanyOverviewAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        if (!result.Success || result.Data is null) return BadRequest(result);
        Response.Headers.CacheControl = "no-store";
        return File(result.Data.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetCompanyOverviewQuery query, CancellationToken ct)
    {
        if (!CompanyOverviewAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
