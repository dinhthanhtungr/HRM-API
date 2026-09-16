using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetSupplierDetail;

public sealed class GetSupplierDetailQuery : PaginationQuery, IRequest<SupplierDetailDto?>
{
    public GetSupplierDetailQuery(Guid supplierId) => SupplierId = supplierId;
    public Guid SupplierId { get; }
}
