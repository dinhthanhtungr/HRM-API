using HRM.Application.Features.CRM.CustomerCare.Commands.TransferCustomers;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferCustomers;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferSourceEmployees;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferTargetEmployees;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferWorkspace;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransfers;
using HRM.Application.Features.CRM.CustomerCare.Queries.ResolveTransferSource;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

/// <summary>
/// API chuyển giao khách hàng giữa các sale trong CRM CustomerCare.
/// Handler Application chịu trách nhiệm kiểm tra quyền leader/full-view, company scope và ghi audit log.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/crm/customer-transfers")]
public sealed class CustomerTransfersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomerTransfersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Tìm sale nguồn đang có khách/lead để chuyển giao.
    /// </summary>
    [HttpGet("source-employees")]
    public async Task<IActionResult> GetTransferSourceEmployees(
        [FromQuery] GetCustomerTransferSourceEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Tìm sale nhận hợp lệ cho chuyển giao.
    /// </summary>
    [HttpGet("target-employees")]
    public async Task<IActionResult> GetTransferTargetEmployees(
        [FromQuery] GetCustomerTransferTargetEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Tìm khách/lead nguồn để chuyển giao theo sale nguồn hoặc khách cụ thể.
    /// </summary>
    [HttpGet("customers")]
    public async Task<IActionResult> GetTransferCustomers(
        [FromQuery] GetCustomerTransferCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Lấy dữ liệu cho màn chuyển giao mới: sale nguồn, khách theo nguồn, sale nhận và summary.
    /// </summary>
    [HttpGet("workspace")]
    public async Task<IActionResult> GetTransferWorkspace(
        [FromQuery] GetCustomerTransferWorkspaceQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Chuyển giao theo flow mới: FE chỉ gửi nguồn/khách/đích, BE tự xử lý lead và khách đã sale.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteTransfer(
        [FromBody] ExecuteCustomerTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ExecuteCustomerTransferCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Chuyển giao một lô lead hoặc customer đã sale từ một sale sang sale khác.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TransferCustomers(
        [FromBody] TransferCustomersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new TransferCustomersCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Resolve sale/group nguồn khi FE chọn danh sách customer trước rồi mới chọn nơi nhận.
    /// </summary>
    [HttpPost("resolve-source")]
    public async Task<IActionResult> ResolveTransferSource(
        [FromBody] ResolveTransferSourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ResolveTransferSourceQuery(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Lấy lịch sử chuyển giao customer trong visibility scope hiện tại.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomerTransfers(
        [FromQuery] GetCustomerTransfersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy lịch sử chuyển giao của một customer cụ thể nếu người dùng có quyền xem customer đó.
    /// </summary>
    [HttpGet("/api/v1/crm/customers/{customerId:guid}/transfers")]
    public async Task<IActionResult> GetCustomerTransfersByCustomer(
        Guid customerId,
        [FromQuery] GetCustomerTransfersQuery query,
        CancellationToken cancellationToken)
    {
        var scopedQuery = new GetCustomerTransfersQuery
        {
            CustomerId = customerId,
            FromEmployeeId = query.FromEmployeeId,
            ToEmployeeId = query.ToEmployeeId,
            TransferType = query.TransferType,
            CreatedFrom = query.CreatedFrom,
            CreatedTo = query.CreatedTo,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            Keyword = query.Keyword,
            SortBy = query.SortBy,
            SortDirection = query.SortDirection
        };
        var result = await _sender.Send(scopedQuery, cancellationToken);
        return Ok(result);
    }
}
