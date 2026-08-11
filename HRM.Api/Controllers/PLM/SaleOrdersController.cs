using System.Text.Json;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Commands.ApproveSaleOrder;
using HRM.Application.Features.PLM.SaleOrders.Commands.CancelSaleOrder;
using HRM.Application.Features.PLM.SaleOrders.Commands.ConfirmSaleOrderDelivery;
using HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder;
using HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrderWithAttachments;
using HRM.Application.Features.PLM.SaleOrders.Commands.PauseSaleOrderDelivery;
using HRM.Application.Features.PLM.SaleOrders.Commands.UpdateSaleOrder;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrderCustomerContext;
using HRM.Application.Features.PLM.SaleOrders.Queries.GetCustomerProductStock;
using HRM.Application.Features.PLM.SaleOrders.Queries.GetLastSaleOrderByCustomer;
using HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrderById;
using HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/sale-orders")]
public sealed class SaleOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public SaleOrdersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    public async Task<IActionResult> CreateSaleOrder(
        [FromForm(Name = "request")] string requestJson,
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken = default)
    {
        CreateSaleOrderRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<CreateSaleOrderRequest>(
                requestJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Field request không phải JSON hợp lệ." });
        }

        if (request is null)
        {
            return BadRequest(new { message = "Thiếu dữ liệu tạo SaleOrder." });
        }

        var uploadFiles = (files ?? new List<IFormFile>())
            .Select(file => new AttachmentUploadFile(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length))
            .ToList();

        try
        {
            var result = uploadFiles.Count == 0
                ? await _sender.Send(new CreateSaleOrderCommand(request), cancellationToken)
                : await _sender.Send(
                    new CreateSaleOrderWithAttachmentsCommand(request, uploadFiles),
                    cancellationToken);

            return result.Success
                ? StatusCode(StatusCodes.Status201Created, result)
                : BadRequest(result);
        }
        finally
        {
            foreach (var uploadFile in uploadFiles)
            {
                await uploadFile.Stream.DisposeAsync();
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSaleOrders(
        [FromQuery] GetSaleOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{merchandiseOrderId:guid}")]
    public async Task<IActionResult> GetSaleOrderById(
        [FromRoute] Guid merchandiseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleOrderByIdQuery(merchandiseOrderId), cancellationToken);
        return result is null
            ? NotFound(new { message = "Merchandise order not found." })
            : Ok(result);
    }

    [HttpGet("last-by-customer")]
    public async Task<IActionResult> GetLastSaleOrderByCustomerId(
        [FromQuery] Guid customerId,
        [FromQuery] Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLastSaleOrderByCustomerQuery(customerId, productId), cancellationToken);
        return result is null
            ? NotFound(new { message = "Merchandise order not found." })
            : Ok(result);
    }

    /// <summary>
    /// Lấy tồn thành phẩm có thể truy vết về MFG của customer theo product và VA lot.
    /// </summary>
    [HttpGet("customer-product-stock")]
    public async Task<IActionResult> GetCustomerProductStock(
        [FromQuery] Guid customerId,
        [FromQuery] Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetCustomerProductStockQuery(customerId, productId),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("customer-context/{customerId:guid}")]
    public async Task<IActionResult> GetSaleOrderCustomerContext(
        [FromRoute] Guid customerId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetSaleOrderCustomerContextQuery(customerId),
            cancellationToken);
        return result is null
            ? NotFound(new { message = "Customer not found or not accessible." })
            : Ok(result);
    }

    [HttpPatch("{merchandiseOrderId:guid}")]
    public async Task<IActionResult> UpdateSaleOrderInformation(
        [FromRoute] Guid merchandiseOrderId,
        [FromBody] UpdateSaleOrderRequest request,
        CancellationToken cancellationToken)
    {
        request = request with { MerchandiseOrderId = merchandiseOrderId };
        var result = await _sender.Send(new UpdateSaleOrderCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{merchandiseOrderId:guid}/approve")]
    [Authorize(Policy = PlmPolicies.ApproveSaleOrder)]
    public async Task<IActionResult> UpdateApproveStatus(
        [FromRoute] Guid merchandiseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ApproveSaleOrderCommand(merchandiseOrderId), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{merchandiseOrderId:guid}/cancel")]
    public async Task<IActionResult> CancelSaleOrder(
        [FromRoute] Guid merchandiseOrderId,
        [FromBody] CancelSaleOrderRequest request,
        CancellationToken cancellationToken)
    {
        request = request with { MerchandiseOrderId = merchandiseOrderId };
        var result = await _sender.Send(new CancelSaleOrderCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{merchandiseOrderId:guid}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(
        [FromRoute] Guid merchandiseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ConfirmSaleOrderDeliveryCommand(merchandiseOrderId),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{merchandiseOrderId:guid}/pause-delivery")]
    public async Task<IActionResult> PauseDelivery(
        [FromRoute] Guid merchandiseOrderId,
        [FromBody] PauseSaleOrderDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        request = request with { MerchandiseOrderId = merchandiseOrderId };
        var result = await _sender.Send(new PauseSaleOrderDeliveryCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
