using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Domain.Entities.MaterialSchema;
namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

internal static class MaterialProcessingProjection
{
    public static MaterialProcessingDto ToDto(MaterialProcessingFormula row, Material? source = null)
    {
        var total = row.Details.Sum(x => x.QuantityKg);
        return new(row.MaterialProcessingFormulaId, row.ExternalId, row.ProcessingType,
            row.MaterialId, row.Material.ExternalId ?? "", row.Material.Name ?? "", row.TotalQuantityRequest,
            row.NumOfBatches, row.StepOfProduct, row.ManufacturingDate,
            row.Status, row.Note, row.Revision, row.CreatedDate, row.UpdatedDate,
            row.Details.OrderBy(x => x.LineNo).Select(x => new MaterialProcessingLineDto(
                x.MaterialProcessingFormulaDetailId, x.MaterialId, x.LineNo,
                x.MaterialCodeSnapshot, x.MaterialNameSnapshot, x.QuantityKg,
                total == 0 ? 0 : decimal.Round(x.QuantityKg / total * 100, 6),
                x.Material?.CategoryId, x.Material?.Unit, decimal.Round(x.QuantityKg / (row.NumOfBatches ?? 1), 3), x.LotNo, x.itemType)).ToList())
        { QcCheck = row.QcCheck, BaseMaterial = source is null ? null : new(source.MaterialId, source.ExternalId ?? "", source.Name ?? "", CategoryId: source.CategoryId, Unit: source.Unit) };
    }
}
