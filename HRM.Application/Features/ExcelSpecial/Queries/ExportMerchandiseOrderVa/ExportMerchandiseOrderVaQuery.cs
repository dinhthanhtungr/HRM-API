using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMerchandiseOrderVa;

/// <summary>Xuất mã đơn và VA trên phiếu giao hàng; ngày lọc là ngày tạo đơn, bỏ lọc để lấy hết.</summary>
public sealed record ExportMerchandiseOrderVaQuery : IRequest<OperationResult<byte[]>>
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public Guid[]? MerchandiseOrderIds { get; init; }
}
