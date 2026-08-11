using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Administration.AssignEmployeeRole;
using HRM.Application.Features.Employees.Administration.CreateEmployeeAccount;
using HRM.Application.Features.Employees.Administration.CreateEmployeeRole;
using HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;
using HRM.Application.Features.Employees.Administration.GetEmployeeRoleLookup;
using HRM.Application.Features.Employees.Administration.RevokeEmployeeRole;
using HRM.Application.Features.Employees.Commands.CreateEmployee;
using HRM.Application.Features.Employees.Queries.GetCompanyLookup;
using HRM.Application.Features.Employees.Queries.GetEmployeeBasicInfoById;
using HRM.Application.Features.Employees.Queries.GetEmployeeById;
using HRM.Application.Features.Employees.Queries.GetEmployeeDropdown;
using HRM.Application.Features.Employees.Queries.GetEmployeePageQuery;
using HRM.Application.Features.Employees.Queries.GetPartLookup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Domain.Security.Rules.Roles;

namespace HRM.Domain.Entities.Controllers.Employees;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
public sealed class EmployeesController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("companies/lookup")]
    public async Task<IActionResult> GetCompanyLookup(
        [FromQuery] GetCompanyLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("parts/lookup")]
    public async Task<IActionResult> GetPartLookup(
        [FromQuery] GetEmployeePartLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("roles/lookup")]
    public async Task<IActionResult> GetRoleLookup(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetEmployeeRoleLookupQuery(),
            cancellationToken);
        return ToAdministrationActionResult(result);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateEmployeeRoleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return ToAdministrationActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployeePage(
        [FromQuery] GetEmployeePageQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/basic-info")]
    public async Task<IActionResult> GetEmployeeBasicInfo(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetEmployeeBasicInfoByIdQuery(id),
            cancellationToken);

        return result is null
            ? NotFound(new { message = "Employee not found." })
            : Ok(result);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetEmployeeLookup(
        [FromQuery] GetEmployeeLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("groups/{groupId:guid}/lookup")]
    public async Task<IActionResult> GetEmployeeLookupByGroup(
        [FromRoute] Guid groupId,
        [FromQuery] GetEmployeeLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetEmployeeLookupQuery
        {
            GroupId = groupId,
            PartId = query.PartId,
            Search = query.Search,
            Keyword = query.Keyword,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDirection = query.SortDirection
        }, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetEmployeeByIdQuery(id), cancellationToken);
        return result is null
            ? NotFound(new { message = "Employee not found." })
            : Ok(result);
    }

    [HttpGet("{employeeId:guid}/account-permissions")]
    public async Task<IActionResult> GetAccountPermissions(
        [FromRoute] Guid employeeId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetEmployeeAccountPermissionsQuery(employeeId),
            cancellationToken);
        return ToAdministrationActionResult(result);
    }

    [HttpPost("{employeeId:guid}/account")]
    public async Task<IActionResult> CreateAccount(
        [FromRoute] Guid employeeId,
        [FromBody] CreateEmployeeAccountCommand command,
        CancellationToken cancellationToken)
    {
        command.EmployeeId = employeeId;
        var result = await _sender.Send(command, cancellationToken);
        return ToAdministrationActionResult(result);
    }

    [HttpPost("{employeeId:guid}/roles")]
    public async Task<IActionResult> AssignRole(
        [FromRoute] Guid employeeId,
        [FromBody] AssignEmployeeRoleCommand command,
        CancellationToken cancellationToken)
    {
        command.EmployeeId = employeeId;
        var result = await _sender.Send(command, cancellationToken);
        return ToAdministrationActionResult(result);
    }

    [HttpDelete("{employeeId:guid}/roles/{roleName}")]
    public async Task<IActionResult> RevokeRole(
        [FromRoute] Guid employeeId,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RevokeEmployeeRoleCommand
            {
                EmployeeId = employeeId,
                RoleName = roleName
            },
            cancellationToken);
        return ToAdministrationActionResult(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleSets.Admins)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    private IActionResult ToAdministrationActionResult<T>(
        EmployeeAdministrationResult<T> result)
    {
        if (result.Success)
        {
            return Ok(result.Data);
        }

        var error = new { message = result.Message };
        return result.Error switch
        {
            EmployeeAdministrationError.Forbidden =>
                StatusCode(StatusCodes.Status403Forbidden, error),
            EmployeeAdministrationError.NotFound => NotFound(error),
            EmployeeAdministrationError.Conflict => Conflict(error),
            _ => BadRequest(error)
        };
    }
}
