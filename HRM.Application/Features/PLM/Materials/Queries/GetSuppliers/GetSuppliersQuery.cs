using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetSuppliers;

public sealed class GetSuppliersQuery : PaginationQuery, IRequest<PagedResult<SupplierSummaryDto>>;
