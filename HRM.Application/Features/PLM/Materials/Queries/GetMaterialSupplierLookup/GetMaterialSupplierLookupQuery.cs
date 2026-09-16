using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialSupplierLookup;

public sealed class GetMaterialSupplierLookupQuery
    : IRequest<IReadOnlyList<MaterialSupplierLookupDto>>
{
    public string? Keyword { get; init; }
    public Guid? MaterialId { get; init; }
    public int PageSize { get; init; } = 50;

    internal int NormalizedPageSize => PageSize < 1 ? 50 : Math.Min(PageSize, 100);
}
