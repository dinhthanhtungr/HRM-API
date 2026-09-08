using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Boms.Models;

internal sealed record BomResolvedItem(
    ItemType ItemType,
    Guid ItemId,
    Guid CategoryId,
    decimal Quantity,
    string Unit,
    string? Code,
    string? Name,
    string? Note);
