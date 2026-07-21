using HRM.Application.Features.Work.ActivityBoard.Dtos;
using HRM.Application.Features.Work.ActivityBoard.Queries.GetWorkActivityBoardCustomer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Work;

/// <summary>
/// API đọc dữ liệu cho màn hình work activity board.
/// Endpoint này chỉ trả danh sách khách hàng và activity summary cho panel trái.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/work/activity-board")]
public sealed class ActivityBoardController : ControllerBase
{
    private readonly ISender _sender;

    public ActivityBoardController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy danh sách khách hàng có task, work plan hoặc interaction để hiển thị panel trái.
    /// </summary>
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] WorkActivityBoardCustomerListQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetWorkActivityBoardCustomersQuery { Request = query }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

}
