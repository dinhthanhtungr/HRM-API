using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrderWithAttachments;

/// <summary>
/// Tạo SaleOrder cùng PO; nhóm duyệt và Sale thường ngoài AC/HN được tự động duyệt sau khi upload thành công.
/// </summary>
public sealed record CreateSaleOrderWithAttachmentsCommand(
    CreateSaleOrderRequest Request,
    IReadOnlyList<AttachmentUploadFile> Files)
    : IRequest<OperationResult<CreateSaleOrderResultDto>>;
