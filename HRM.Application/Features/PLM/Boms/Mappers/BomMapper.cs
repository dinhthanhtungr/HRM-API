using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Models;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Boms.Mappers;

internal static class BomMapper
{
    internal static List<BomVersionItem> CreateVersionItems(
        Guid versionId,
        IReadOnlyList<BomResolvedItem> items)
        => items
            .Select((item, index) => new BomVersionItem
            {
                BomVersionItemId = Guid.CreateVersion7(),
                BomVersionId = versionId,
                LineNo = index + 1,
                ItemType = item.ItemType,
                MaterialId = item.ItemType == ItemType.Material
                    ? item.ItemId
                    : null,
                ComponentProductId = item.ItemType == ItemType.Product
                    ? item.ItemId
                    : null,
                CategoryId = item.CategoryId,
                Quantity = item.Quantity,
                Unit = item.Unit,
                MaterialExternalIdSnapshot = item.Code,
                MaterialNameSnapshot = item.Name,
                Note = item.Note
            })
            .ToList();

    /// <summary>
    /// Giữ contract response của các lệnh ghi: BomVersionItemId không được expose.
    /// GET detail vẫn trả id thật qua projection riêng của query.
    /// </summary>
    internal static BomVersionDto ToWriteResponseDto(
        BomDefinition definition,
        BomVersion version,
        IEnumerable<BomVersionItem> items)
        => new()
        {
            BomDefinitionId = definition.BomDefinitionId,
            BomVersionId = version.BomVersionId,
            ProductId = definition.ProductId,
            Code = definition.Code,
            Name = definition.Name,
            BomType = definition.BomType,
            VersionNo = version.VersionNo,
            Status = version.Status,
            BaseOutputQuantity = version.BaseOutputQuantity,
            OutputUnit = version.OutputUnit,
            EffectiveFrom = version.EffectiveFrom,
            EffectiveTo = version.EffectiveTo,
            ChangeReason = version.ChangeReason,
            Note = version.Note,
            Items = items
                .OrderBy(item => item.LineNo)
                .Select((item, index) => new BomItemDto
                {
                    LineNo = index + 1,
                    ItemType = item.ItemType,
                    ItemId = item.MaterialId ?? item.ComponentProductId ?? Guid.Empty,
                    CategoryId = item.CategoryId ?? Guid.Empty,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    ItemCode = item.MaterialExternalIdSnapshot,
                    ItemName = item.MaterialNameSnapshot,
                    Note = item.Note
                })
                .ToList()
        };
}
