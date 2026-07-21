using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransfers;

/// <summary>
/// Feature CRM CustomerCare - lấy lịch sử chuyển giao khách hàng theo visibility scope hiện tại.
/// Query trả thông tin từ sale nào sang sale nào, group nào, người tạo log và các customer visible trong log.
/// </summary>
public sealed class GetCustomerTransfersQuery : PaginationQuery, IRequest<PagedResult<CustomerTransferLogDto>>
{
    public Guid? CustomerId { get; init; }
    public Guid? FromEmployeeId { get; init; }
    public Guid? ToEmployeeId { get; init; }
    public TransferType? TransferType { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
}
