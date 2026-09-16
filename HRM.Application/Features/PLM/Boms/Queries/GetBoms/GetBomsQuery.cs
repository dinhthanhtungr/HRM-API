using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBoms;

/// <summary>Lấy danh sách BOM active theo loại trong công ty hiện tại.</summary>
public sealed class GetBomsQuery : IRequest<IReadOnlyList<BomListItemDto>>
{
    public Guid? ProductId { get; init; }
    public string? Keyword { get; init; }
    public BomType BomType { get; init; } = BomType.Engineering;
}
