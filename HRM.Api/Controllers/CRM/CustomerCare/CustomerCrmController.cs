using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Commands.ArchiveCustomerInteraction;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.AddCustomerFollowUpTaskAssignee;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.AddCustomerFollowUpTaskGroupAssignees;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.ArchiveCustomerFollowUpTask;
using HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomerInteraction;
using HRM.Application.Features.CRM.CustomerCare.Commands.ArchiveCustomerWorkPlan;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.CompleteCustomerFollowUpTask;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.CreateCustomerFollowUpTask;
using HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomerWorkPlan;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.RemoveCustomerFollowUpTaskAssignee;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.UpdateCustomerFollowUpTask;
using HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomerWorkPlan;
using HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomerInteraction;
using HRM.Application.Features.CRM.CustomerCare.Queries.CustomerCrmAnalytics;
using HRM.Application.Features.CRM.CustomerCare.Queries.CustomerFollowUpTasks;
using HRM.Application.Features.CRM.CustomerCare.Queries.CustomerWorkPlans;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerInteractionById;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerInteractionsByCustomer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Domain.Security.Rules.Roles;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummaryBatch;
using HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummary;
using HRM.Application.Features.CRM.InteractionSummaries.Queries.GetLatestCustomerInteractionSummary;
using HRM.Application.Features.CRM.InteractionSummaries.Queries.GetCustomerInteractionSummaryQuota;

namespace HRM.Api.Controllers.CRM.CustomerCare;

/// <summary>
/// API CRM thủ công dành cho sale và cấp quản lý.
/// Task/plan được lưu trong WorkTaskSchema; interaction và AI summary dùng entity CRM chuyên biệt.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/crm")]
public sealed class CustomerCrmController : ControllerBase
{
    private readonly ISender _sender;

    public CustomerCrmController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Ghi nhận một lần tương tác với khách hàng và có thể tạo follow-up task từ ngày theo dõi tiếp.
    /// </summary>
    [HttpPost("interactions")]
    public async Task<IActionResult> CreateInteraction([FromBody] CreateCustomerInteractionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateCustomerInteractionCommand { Request = request }, cancellationToken);
        return result.Success ? CreatedAtAction(nameof(GetInteraction), new { interactionId = result.Data }, result) : BadRequest(result);
    }

    /// <summary>
    /// Cập nhật interaction và đồng bộ follow-up task đang liên kết nếu có.
    /// </summary>
    [HttpPatch("interactions/{interactionId:guid}")]
    public async Task<IActionResult> UpdateInteraction(Guid interactionId, [FromBody] UpdateCustomerInteractionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateCustomerInteractionCommand
        {
            InteractionId = interactionId,
            Request = request
        }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Lấy chi tiết interaction trong phạm vi khách hàng người dùng được phép xem.
    /// </summary>
    [HttpGet("interactions/{interactionId:guid}")]
    public async Task<IActionResult> GetInteraction(Guid interactionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerInteractionByIdQuery(interactionId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Archive interaction bằng soft delete; không xóa cứng lịch sử CRM.
    /// </summary>
    [HttpDelete("interactions/{interactionId:guid}")]
    public async Task<IActionResult> ArchiveInteraction(Guid interactionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveCustomerInteractionCommand(interactionId), cancellationToken);
        return result.Success ? NoContent() : NotFound(result);
    }

    /// <summary>
    /// Lấy lịch sử interaction của một khách hàng theo bộ lọc và phân trang.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/interactions")]
    public async Task<IActionResult> GetCustomerInteractions(Guid customerId, [FromQuery] CustomerInteractionQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerInteractionsByCustomerQuery
        {
            CustomerId = customerId,
            Query = query
        }, cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Tạo hoặc lấy lại bản tóm tắt AI cho lịch sử interaction của một khách hàng.
    /// </summary>
    [HttpPost("customers/{customerId:guid}/ai-summary")]
    public async Task<IActionResult> GenerateAiSummary(Guid customerId, [FromBody] GenerateCustomerInteractionAiSummaryRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateCustomerInteractionSummaryCommand { CustomerId = customerId, Request = request }, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Tạo AI summary theo lô cho các khách hàng hợp lệ trong visibility scope hiện tại.
    /// </summary>
    [HttpPost("ai-summary/batch")]
    public async Task<IActionResult> GenerateAiSummaryBatch([FromBody] GenerateCustomerInteractionAiSummaryBatchRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateCustomerInteractionSummaryBatchCommand { Request = request }, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Lấy bản AI summary mới nhất mà người dùng có quyền xem của khách hàng.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/ai-summary/latest")]
    public async Task<IActionResult> GetLatestAiSummary(Guid customerId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLatestCustomerInteractionSummaryQuery(customerId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Lấy trạng thái hạn mức gọi Gemini hiện tại để FE quyết định khả năng tạo summary.
    /// </summary>
    [HttpGet("ai-summary/quota")]
    public async Task<IActionResult> GetAiSummaryQuota(CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetCustomerInteractionSummaryQuotaQuery(), cancellationToken));

    /// <summary>
    /// Tạo follow-up task CRM và liên kết task với khách hàng bằng WorkTaskReference.
    /// </summary>
    [HttpPost("follow-up-tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateCustomerFollowUpTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateCustomerFollowUpTaskCommand { Request = request }, cancellationToken);
        return result.Success ? Created($"/api/v1/crm/follow-up-tasks/{result.Data}", result) : BadRequest(result);
    }

    /// <summary>
    /// Cập nhật các trường được phép của follow-up task.
    /// </summary>
    [HttpPatch("follow-up-tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateCustomerFollowUpTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateCustomerFollowUpTaskCommand
        {
            TaskId = taskId,
            Request = request
        }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Lấy chi tiết follow-up task trong company và customer visibility scope hiện tại.
    /// </summary>
    [HttpGet("follow-up-tasks/{taskId:guid}")]
    public async Task<IActionResult> GetTask(Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerFollowUpTaskQuery(taskId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Archive follow-up task bằng IsActive thay vì xóa cứng.
    /// </summary>
    [HttpDelete("follow-up-tasks/{taskId:guid}")]
    public async Task<IActionResult> ArchiveTask(Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveCustomerFollowUpTaskCommand(taskId), cancellationToken);
        return result.Success ? NoContent() : NotFound(result);
    }

    /// <summary>
    /// Hoàn tất hoặc đóng follow-up task và ghi nhận người thực hiện, thời điểm cùng ghi chú.
    /// </summary>
    [HttpPost("follow-up-tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteTask(Guid taskId, [FromBody] CompleteCustomerFollowUpTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CompleteCustomerFollowUpTaskCommand
        {
            TaskId = taskId,
            Request = request
        }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Lấy các task được giao trực tiếp hoặc giao hỗ trợ cho nhân viên hiện tại.
    /// </summary>
    [HttpGet("follow-up-tasks/mine")]
    public async Task<IActionResult> GetMyTasks([FromQuery] CustomerFollowUpTaskQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyCustomerFollowUpTasksQuery { Query = query }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Lấy follow-up task của một khách hàng theo bộ lọc và phân trang.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/follow-up-tasks")]
    public async Task<IActionResult> GetCustomerTasks(Guid customerId, [FromQuery] CustomerFollowUpTaskQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerFollowUpTasksByCustomerQuery
        {
            CustomerId = customerId,
            Query = query
        }, cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Gán thêm một nhân viên hỗ trợ vào follow-up task.
    /// </summary>
    [HttpPost("follow-up-tasks/{taskId:guid}/assignees")]
    public async Task<IActionResult> AddTaskAssignee(Guid taskId, [FromBody] AddCustomerFollowUpTaskAssigneeRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AddCustomerFollowUpTaskAssigneeCommand
        {
            TaskId = taskId,
            Request = request
        }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Gán các thành viên hợp lệ của một group vào follow-up task.
    /// </summary>
    [HttpPost("follow-up-tasks/{taskId:guid}/assignees/group")]
    public async Task<IActionResult> AddTaskGroupAssignees(Guid taskId, [FromBody] AddCustomerFollowUpTaskGroupAssigneesRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AddCustomerFollowUpTaskGroupAssigneesCommand
        {
            TaskId = taskId,
            Request = request
        }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Lấy danh sách assignee đang hoạt động của follow-up task.
    /// </summary>
    [HttpGet("follow-up-tasks/{taskId:guid}/assignees")]
    public async Task<IActionResult> GetTaskAssignees(Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerFollowUpTaskAssigneesQuery(taskId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Ngừng gán một nhân viên khỏi task bằng soft remove.
    /// </summary>
    [HttpDelete("follow-up-tasks/{taskId:guid}/assignees/{employeeId:guid}")]
    public async Task<IActionResult> RemoveTaskAssignee(Guid taskId, Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveCustomerFollowUpTaskAssigneeCommand(taskId, employeeId), cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Tạo kế hoạch chăm sóc khách hàng dài hạn.
    /// </summary>
    [HttpPost("work-plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateCustomerWorkPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateCustomerWorkPlanCommand { Request = request }, cancellationToken);
        return result.Success ? Created($"/api/v1/crm/work-plans/{result.Data}", result) : BadRequest(result);
    }

    /// <summary>
    /// Cập nhật các trường nghiệp vụ được phép của work plan.
    /// </summary>
    [HttpPatch("work-plans/{planId:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid planId, [FromBody] UpdateCustomerWorkPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateCustomerWorkPlanCommand
        {
            PlanId = planId,
            Request = request
        }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    /// <summary>
    /// Lấy chi tiết work plan trong phạm vi truy cập hiện tại.
    /// </summary>
    [HttpGet("work-plans/{planId:guid}")]
    public async Task<IActionResult> GetPlan(Guid planId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerWorkPlanQuery(planId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Archive work plan bằng IsActive thay vì xóa cứng.
    /// </summary>
    [HttpDelete("work-plans/{planId:guid}")]
    public async Task<IActionResult> ArchivePlan(Guid planId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveCustomerWorkPlanCommand(planId), cancellationToken);
        return result.Success ? NoContent() : NotFound(result);
    }

    /// <summary>
    /// Lấy danh sách work plan của một khách hàng theo bộ lọc và phân trang.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/work-plans")]
    public async Task<IActionResult> GetCustomerPlans(Guid customerId, [FromQuery] CustomerWorkPlanQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerWorkPlansByCustomerQuery
        {
            CustomerId = customerId,
            Query = query
        }, cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Hợp nhất task, interaction và work plan thành dữ liệu calendar; không dùng bảng calendar riêng.
    /// </summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] CustomerCrmCalendarQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerCrmCalendarQuery { Query = query }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Lấy dữ liệu tổng quan hoạt động CRM theo từng khách hàng.
    /// </summary>
    [HttpGet("activity-headers")]
    public async Task<IActionResult> GetActivityHeaders([FromQuery] CustomerCrmActivityHeaderQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerCrmActivityHeadersQuery { Query = query }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Lấy dashboard cá nhân của sale đang đăng nhập.
    /// </summary>
    [HttpGet("dashboard/sale")]
    public async Task<IActionResult> GetSaleDashboard(CancellationToken cancellationToken)
        => Ok((await _sender.Send(new GetSaleCrmDashboardQuery(), cancellationToken)).Data);

    /// <summary>
    /// Lấy dashboard trong visibility scope của leader; endpoint yêu cầu role quản lý sale.
    /// </summary>
    [HttpGet("dashboard/leader")]
    [Authorize(Roles = RoleSets.SaleLeaders)]
    public async Task<IActionResult> GetLeaderDashboard(CancellationToken cancellationToken)
        => Ok((await _sender.Send(new GetLeaderCrmDashboardQuery(), cancellationToken)).Data);

    /// <summary>
    /// Lấy dashboard tổng quan dành cho director/admin; endpoint yêu cầu role quản trị.
    /// </summary>
    [HttpGet("dashboard/director")]
    [Authorize(Roles = RoleSets.Admins)]
    public async Task<IActionResult> GetDirectorDashboard(CancellationToken cancellationToken)
        => Ok((await _sender.Send(new GetDirectorCrmDashboardQuery(), cancellationToken)).Data);

    /// <summary>
    /// Tổng hợp hoạt động, liên hệ, task và doanh số khách hàng trong kỳ báo cáo.
    /// </summary>
    [HttpGet("activity-report")]
    public async Task<IActionResult> GetActivityReport([FromQuery] CustomerActivityCalendarReportQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerActivityCalendarReportQuery { Query = query }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
