using HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomer;
using HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomer;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomers;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerById;
using HRM.Application.Features.CRM.Customers.Queries.GetCustomerLookup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

[ApiController]
[Authorize]
[Route("api/v1/crm/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Tạo hồ sơ khách hàng tiềm năng trong công ty hiện tại và claim cho sale đang thao tác.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateCustomerCommand(request), cancellationToken);
        return result.Success && result.Data is not null
            ? CreatedAtAction(nameof(GetCustomerById), new { customerId = result.Data.CustomerId }, result)
            : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] GetCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns the editable customer profile inside the current viewer's customer scope.
    /// </summary>
    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> GetCustomerById(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerByIdQuery(customerId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>
    /// Patches a customer profile and upserts address, contact, and note children.
    /// </summary>
    [HttpPatch("{customerId:guid}")]
    public async Task<IActionResult> UpdateCustomer(
        Guid customerId,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateCustomerCommand(customerId, request), cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetCustomerLookup(
        [FromQuery] GetCustomerLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }
}
