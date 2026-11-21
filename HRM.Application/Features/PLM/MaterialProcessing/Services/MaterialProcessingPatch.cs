using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Domain.Entities.MaterialSchema;
namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

public static class MaterialProcessingPatch
{
    public static (string? Error, SaveMaterialProcessingRequest? Request) Merge(MaterialProcessingFormula row, PatchMaterialProcessingRequest patch)
    {
        var clear = patch.ClearFields ?? [];
        if (clear.Any(x => x is not ("note" or "qcCheck" or "numOfBatches" or "manufacturingDate")) ||
            clear.Distinct().Count() != clear.Count ||
            (clear.Contains("note") && patch.Note is not null) ||
            (clear.Contains("qcCheck") && patch.QcCheck is not null) ||
            (clear.Contains("numOfBatches") && patch.NumOfBatches.HasValue) ||
            (clear.Contains("stepOfProduct") && patch.StepOfProduct.HasValue) ||
            (clear.Contains("manufacturingDate") && patch.ManufacturingDate.HasValue)) return ("InvalidPatch", null);
        return (null, new(patch.ProcessingType ?? row.ProcessingType, patch.MaterialId ?? row.MaterialId,
            patch.TotalQuantityRequest ?? row.TotalQuantityRequest,
            clear.Contains("numOfBatches") ? null : patch.NumOfBatches ?? row.NumOfBatches,
            clear.Contains("stepOfProduct") ? null : patch.StepOfProduct ?? row.StepOfProduct,
            clear.Contains("manufacturingDate") ? null : patch.ManufacturingDate ?? row.ManufacturingDate,
            patch.Status ?? row.Status, clear.Contains("note") ? null : patch.Note ?? row.Note,
            patch.Details ?? row.Details.OrderBy(x => x.LineNo).Select(x => new MaterialProcessingLineRequest(x.MaterialId, x.QuantityKg, x.LotNo, x.itemType)).ToList(),
            patch.ExpectedRevision, clear.Contains("qcCheck") ? null : patch.QcCheck ?? row.QcCheck));
    }
}
