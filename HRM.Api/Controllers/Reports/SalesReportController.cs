using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportBody;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportOrders;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportCustomers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Reports;

[ApiController]
[Authorize]
[Route("api/v1/reports/sales")]
public sealed class SalesReportController(ISender sender, ICurrentUser user) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] GetSalesReportOrdersQuery query, CancellationToken ct)
    {
        if (!user.CompanyId.HasValue || user.CompanyId == Guid.Empty
            || (query.CompanyId.HasValue && query.CompanyId != user.CompanyId))
            return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers([FromQuery] GetSalesReportCustomersQuery query, CancellationToken ct)
    {
        if (!user.CompanyId.HasValue || user.CompanyId == Guid.Empty
            || (query.CompanyId.HasValue && query.CompanyId != user.CompanyId))
            return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("body")]
    [Authorize(Roles = ApplicationRoleSets.Reports.SalesDashboardReaderRolesCsv)]
    public async Task<IActionResult> GetBody([FromQuery] GetSalesReportBodyQuery query, CancellationToken ct)
    {
        if (!user.CompanyId.HasValue || user.CompanyId == Guid.Empty
            || (query.CompanyId.HasValue && query.CompanyId != user.CompanyId))
            return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("header")]
    [Authorize(Roles = ApplicationRoleSets.Reports.SalesScopedReaderRolesCsv)]
    public async Task<IActionResult> GetHeader([FromQuery] GetSalesReportHeaderQuery query, CancellationToken ct)
    {
        if (!user.CompanyId.HasValue || user.CompanyId == Guid.Empty
            || (query.CompanyId.HasValue && query.CompanyId != user.CompanyId))
            return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
