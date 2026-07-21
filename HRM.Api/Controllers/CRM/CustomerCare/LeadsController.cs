using HRM.Application.Features.CRM.CustomerCare.Commands.CancelLeadClaim;
using HRM.Application.Features.CRM.CustomerCare.Commands.ClaimLead;
using HRM.Application.Features.CRM.CustomerCare.Commands.ConvertLead;
using HRM.Application.Features.CRM.CustomerCare.Commands.UpdateLead;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetLeadClaims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

/// <summary>
/// API quản lý khách hàng tiềm năng trong CRM CustomerCare.
/// Controller chỉ bind request; nghiệp vụ claim, chỉnh sửa và convert nằm ở Application.
/// Tạo mới lead dùng chung POST /api/v1/crm/customers để tránh nhiều luồng tạo customer khác nhau.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/crm/leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly ISender _sender;

    public LeadsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy danh sách người đang claim lead, thời hạn claim và interaction gần nhất của từng người với khách.
    /// </summary>
    [HttpGet("{customerId:guid}/claims")]
    public async Task<IActionResult> GetLeadClaims(
        Guid customerId,
        [FromQuery] int interactionLimit,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLeadClaimsQuery(customerId, interactionLimit), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Chỉnh sửa lead đang nằm trong visibility scope, không convert lead trong endpoint này.
    /// </summary>
    [HttpPatch("{customerId:guid}")]
    public async Task<IActionResult> UpdateLead(
        Guid customerId,
        [FromBody] UpdateLeadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateLeadCommand(customerId, request), cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Nhận phụ trách lead bằng CustomerClaim Work.
    /// </summary>
    [HttpPost("{customerId:guid}/claim")]
    public async Task<IActionResult> ClaimLead(
        Guid customerId,
        [FromBody] ClaimLeadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ClaimLeadCommand(customerId, request), cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Hủy một người đang chăm sóc lead bằng cách soft-disable claim Work.
    /// </summary>
    [HttpDelete("{customerId:guid}/claims/{claimId:guid}")]
    public async Task<IActionResult> CancelLeadClaim(
        Guid customerId,
        Guid claimId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelLeadClaimCommand(customerId, claimId), cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Chuyển lead thành khách đã sale bằng CustomerAssignment.
    /// </summary>
    [HttpPost("{customerId:guid}/convert")]
    public async Task<IActionResult> ConvertLead(
        Guid customerId,
        [FromBody] ConvertLeadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ConvertLeadCommand(customerId, request), cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }
}
