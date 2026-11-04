using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.Purchasing.MaterialRequirements.Queries.GetMaterialRequirementDemands;

public sealed class GetMaterialRequirementDemandsQuery : PaginationQuery,
    IRequest<OperationResult<MaterialRequirementDemandsResponse>>
{
    public Guid MaterialId { get; set; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? SaleOrderId { get; init; }
    public Guid? ProductionOrderId { get; init; }
}
