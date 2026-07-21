using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.ResolveTransferSource;

/// <summary>
/// Feature CRM CustomerCare - resolve sale/group nguồn khi FE chọn danh sách customer trước khi chuyển giao.
/// Query chỉ trả nguồn nếu tất cả customer visible có cùng owner active theo đúng loại chuyển Lead/Saled.
/// </summary>
public sealed record ResolveTransferSourceQuery(ResolveTransferSourceRequest Request)
    : IRequest<OperationResult<TransferSourceResolutionDto>>;
