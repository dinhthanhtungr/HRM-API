using HRM.Application.Features.Employees.Commands.CreateEmployee;
using HRM.Application.Features.Employees.Queries.GetEmployeeBasicInfoById;
using HRM.Application.Features.Employees.Queries.GetEmployeeById;
using HRM.Application.Features.Employees.Queries.GetEmployeeDropdown;
using HRM.Application.Features.Employees.Queries.GetEmployeePageQuery;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Domain.Entities.Controllers.Employees
{
    [ApiController]
    [Route("api/v1/employees")]
    public sealed class EmployeesController : ControllerBase
    {
        private readonly ISender _sender;

        public EmployeesController(ISender sender)
        {
            _sender = sender;
        }

        // GET: api/v1/employees

        [HttpGet]
        public async Task<IActionResult> GetEmployeePageQuery(
            [FromQuery] GetEmployeePageQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            
            if (result is null)
            {
                return NotFound(new { message = "No employees found." });
            }

            return Ok(result);
        }

        [HttpGet("{id:guid}/basic-info")]
        public async Task<IActionResult> GetEmployeeBasicInfo(
            [FromRoute] Guid id, 
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(new GetEmployeeBasicInfoByIdQuery(id), cancellationToken);

            if (result is null)
            {
                return NotFound(new { message = "Employee not found." });
            }

            return Ok(result);
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> GetEmployeeLookupQuery(
            [FromQuery] GetEmployeeLookupQuery query, 
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);

            if (result is null)
            {
                return NotFound(new { message = "No employees found." });
            }

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

            if (result is null)
            {
                return NotFound(new { message = "No employees found in group." });
            }

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(
            [FromRoute] Guid id, 
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(new GetEmployeeByIdQuery(id), cancellationToken);

            if (result is null)
            {
                return NotFound(new { message = "Employee not found." });
            }

            return Ok(result);
        }

        // POST: api/v1/employees

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateEmployeeCommand command, 
            CancellationToken cancellationToken)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
