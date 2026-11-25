using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.PLPU.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUHeader;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUBody;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrders;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrderDetail;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPULines;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUReceipts;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUQc;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUSuppliers;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUFinish;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUFinishExport;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUDrilldown;

namespace HRM.Api.Controllers.Reports;
[ApiController]
[Authorize(Policy = HRM.Api.Security.Authorization.SensitiveDataAuthorization.PolicyName)]
[Route("api/v1/reports/plpu")]
public sealed class PLPUReportController(ISender sender, ICurrentUser user, ICurrentUserPermissionService permissions) : ControllerBase
{
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] HRM.Application.Features.Reports.PLPU.Queries.GetPLPUSearchSuggestionsQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("drilldown")]
    [ProducesResponseType(typeof(PLPUDrilldownDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDrilldown([FromQuery] GetPLPUDrilldownQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("header")]
    [ProducesResponseType(typeof(PLPUPurchaseOverviewSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHeader([FromQuery] GetPLPUHeaderQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("body")]
    public async Task<IActionResult> GetBody([FromQuery] GetPLPUBodyQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("material-activity")]
    [ProducesResponseType(typeof(PLPUMaterialActivityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaterialActivity([FromQuery] GetPLPUMaterialActivityQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] GetPLPUOrdersQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrderDetail(Guid id, CancellationToken ct)
    {
        var query = new GetPLPUOrderDetailQuery { Id = id };
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return !result.Success ? BadRequest(result) : result.Data!.Header.PurchaseOrderId == Guid.Empty ? NotFound() : Ok(result.Data);
    }

    [HttpGet("lines")]
    public async Task<IActionResult> GetLines([FromQuery] GetPLPULinesQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceipts([FromQuery] GetPLPUReceiptsQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("qc")]
    public async Task<IActionResult> GetQc([FromQuery] GetPLPUQcQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers([FromQuery] GetPLPUSuppliersQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("finish")]
    public async Task<IActionResult> GetFinish([FromQuery] GetPLPUFinishQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("finish/export")]
    public async Task<IActionResult> GetFinishExport([FromQuery] GetPLPUFinishExportQuery query, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, query.CompanyId)) return Forbid();
        var result = await sender.Send(query, ct);
        return result.Success ? File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCaoGiaoHangHoanTat_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx") : BadRequest(result);
    }

}
