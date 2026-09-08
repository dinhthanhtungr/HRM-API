using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBoms;

/// <summary>Lấy danh sách E-BOM active trong công ty hiện tại.</summary>
public sealed class GetBomsQuery : IRequest<IReadOnlyList<BomListItemDto>>
{
    public Guid? ProductId { get; init; }
    public string? Keyword { get; init; }
}
