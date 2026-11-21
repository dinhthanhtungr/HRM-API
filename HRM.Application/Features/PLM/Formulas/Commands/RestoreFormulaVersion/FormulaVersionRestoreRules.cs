using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.PLM.Formulas.Commands.RestoreFormulaVersion;

internal static class FormulaVersionRestoreRules
{
    internal static void ApplyHeader(Formula formula, FormulaVersion source, Guid employeeId, DateTime now)
    {
        formula.Name = source.Name;
        // Restoring historical content is not a new Lab approval or delivery.
        formula.Status = FormulaStatus.Draft.ToString();
        formula.CheckBy = null;
        formula.CheckDate = null;
        formula.SentBy = null;
        formula.SentDate = null;
        formula.Note = source.Note;
        formula.TotalPrice = source.TotalPrice;
        formula.ProductionPrice = source.ProductionPrice;
        formula.PresidentPrice = source.PresidentPrice;
        formula.ProfitMarginPrice = source.ProfitMarginPrice;
        formula.UpdatedBy = employeeId;
        formula.UpdatedDate = now;
    }
}
