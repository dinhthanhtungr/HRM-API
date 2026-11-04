using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.Purchasing.MaterialRequirements;

public sealed class GetMaterialPurchaseRequirementsQuery : PaginationQuery,
    IRequest<OperationResult<MaterialPurchaseRequirementsResponse>>
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool OnlyShortages { get; init; } = true;
}
