using HRM.Application.Features.CRM.CustomerCare.Commands.TransferCustomers;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
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
