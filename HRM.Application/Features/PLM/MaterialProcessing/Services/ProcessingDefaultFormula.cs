using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Products;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

internal static class ProcessingDefaultFormula
{
    // Caller holds the company catalog lock and owns the encompassing transaction.
    public static async Task<bool> EnsureAsync(IPLMWriteDbContext db, Product product,
        SampleRequest sample, Material source, string process, Guid employee, DateTime now, CancellationToken ct)
    {
        var code = product.ColourCode!;
        var pattern = code.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var matches = await db.Formulas.Where(x => x.CompanyId == product.CompanyId
            && EF.Functions.ILike(x.ExternalId, pattern, "\\"))
            .Include(x => x.FormulaMaterials).Take(2).ToListAsync(ct);
        if (matches.Count > 1 || matches.Any(x => !x.IsActive || x.ProductId != product.ProductId
            || x.Status == nameof(FormulaStatus.Cancelled))) return false;
        var formula = matches.SingleOrDefault();
        if (formula is null)
        {
            formula = new Formula { FormulaId = Guid.CreateVersion7(), ExternalId = code, Name = "F001",
                ProductId = product.ProductId, Product = product, CompanyId = product.CompanyId,
                Status = nameof(FormulaStatus.Approved), IsActive = true, CreatedBy = employee, CreatedDate = now };
            db.Formulas.Add(formula);
        }
        var lines = formula.FormulaMaterials.Where(x => x.IsActive).ToArray();
        if (lines.Length > 0 && (lines.Length != 1 || lines[0].MaterialId != source.MaterialId
            || lines[0].itemType != ItemType.Material || lines[0].Quantity != 1m)) return false;
        if (lines.Length == 0)
            db.FormulaMaterials.Add(new FormulaMaterial { FormulaMaterialId = Guid.CreateVersion7(),
                FormulaId = formula.FormulaId, Formula = formula, MaterialId = source.MaterialId,
                CategoryId = source.CategoryId, Quantity = 1m, itemType = ItemType.Material,
                Unit = source.Unit, MaterialExternalIdSnapshot = source.ExternalId, MaterialNameSnapshot = source.Name,
                LineNo = 1, IsActive = true });
        var step = MaterialProcessingRules.Step(process);
        var changed = !formula.IsSelect || formula.StepOfProduct != step || lines.Length == 0;
        var selected = await db.Formulas.Where(x => x.ProductId == product.ProductId && x.CompanyId == product.CompanyId
            && x.IsSelect && x.FormulaId != formula.FormulaId).ToListAsync(ct);
        foreach (var other in selected)
        {
            other.IsSelect = false;
            other.UpdatedBy = employee;
            other.UpdatedDate = now;
        }
        formula.IsSelect = true;
        formula.StepOfProduct = step;
        if (changed) { formula.UpdatedBy = employee; formula.UpdatedDate = now; }
        if (sample.FormulaId != formula.FormulaId)
        {
            sample.FormulaId = formula.FormulaId;
            sample.UpdatedBy = employee;
            sample.UpdatedDate = now;
        }
        return true;
    }
}
