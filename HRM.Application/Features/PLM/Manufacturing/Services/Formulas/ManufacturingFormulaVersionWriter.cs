using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingFormulaVersionWriter
{
    /// <summary>Legacy Upsert snapshot vật tư trước sửa, không phải công thức sau sửa.</summary>
    public static ManufacturingFormulaVersion SnapshotBeforeEdit(
        ManufacturingFormula formula,
        DateTime now,
        string? note
    )
    {
        var versionNo = formula.ManufacturingFormulaVersions.Select(x => x.VersionNo).DefaultIfEmpty().Max() + 1;
        formula.Name = $"F{versionNo:D3}";
        foreach (var old in formula.ManufacturingFormulaVersions.Where(x => x.EffectiveTo == null))
        {
            old.EffectiveTo = now;
        }

        var snapshot = new ManufacturingFormulaVersion

        {

            ManufacturingFormulaVersionId = Guid.CreateVersion7(),

            ManufacturingFormulaId = formula.ManufacturingFormulaId,

            VersionNo = versionNo,

            Status = formula.Status,

            EffectiveFrom = now,

            Note = note ?? formula.Note

        };
        snapshot.Items = formula.ManufacturingFormulaMaterials.Where(x => x.IsActive).OrderBy(x => x.LineNo == 0 ? int.MaxValue : x.LineNo)
            .ThenBy(x => x.MaterialExternalIdSnapshot)
            .Select(x => new ManufacturingFormulaVersionItem
        {
            ManufacturingFormulaVersionItemId = Guid.CreateVersion7(),
            ManufacturingFormulaVersionId = snapshot.ManufacturingFormulaVersionId,
            LineNo = x.LineNo,
            MaterialId = x.MaterialId,
            CategoryId = x.CategoryId, // Legacy snapshot omits ProductId/itemType; preserve that storage behavior explicitly.

            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            TotalPrice = x.TotalPrice,
            MaterialNameSnapshot = x.MaterialNameSnapshot ?? string.Empty,
            MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot ?? string.Empty,
            Unit = x.Unit ?? string.Empty
        })
            .ToList();
        formula.ManufacturingFormulaVersions.Add(snapshot);
        return snapshot;
    }
}
