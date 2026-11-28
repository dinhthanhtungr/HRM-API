using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingRequestedSource
{
    internal static async Task<ProductionOrderFormulaOptionDto?> ResolveAsync(IManufacturingDbContext db,
        MfgProductionOrder order, Guid sourceId, CancellationToken ct)
    {
        var option = (await ManufacturingFormulaOptionReader.ReadAsync(db, order, ct))
            .FirstOrDefault(x => x.FormulaId == sourceId);
        if (option is not null) return option;
        // The current VA can remain selected after the product standard changes.
        var own = await db.ProductionSelectVersions.AsNoTracking()
            .Where(x => x.CompanyId == order.CompanyId && x.MfgProductionOrderId == order.MfgProductionOrderId
                && x.ManufacturingFormulaId == sourceId && x.ManufacturingFormula != null
                && x.ManufacturingFormula.CompanyId == order.CompanyId && x.ManufacturingFormula.IsActive)
            .Select(x => new { x.ManufacturingFormula!.ExternalId, x.ManufacturingFormula.CreatedDate, x.ManufacturingFormula.Note })
            .FirstOrDefaultAsync(ct);
        return own is null ? null : new(sourceId, own.ExternalId,
            ManufacturingFormulaOptionReader.ProductionOrigin(own.CreatedDate), order.MfgProductionOrderId, own.CreatedDate, true)
        { Note = own.Note, LabNote = order.LabNote, PlpuNote = order.PlpuNote, Requirement = order.Requirement, QcCheck = order.QcCheck };
    }

    // Called under the parent MFG lock and transaction. A pending source is not production activation.
    internal static async Task<OperationResult> StoreAsync(IManufacturingDbContext db, MfgProductionOrder order,
        ProductionOrderFormulaOptionDto source, Guid employee, DateTime now, CancellationToken ct)
    {
        var open = await db.ProductionSelectVersions.Where(x => x.CompanyId == order.CompanyId &&
            x.MfgProductionOrderId == order.MfgProductionOrderId && x.ValidTo == null).ToListAsync(ct);
        var isVu = source.Origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement;
        var same = isVu ? null : open.FirstOrDefault(x => x.ManufacturingFormulaId == source.FormulaId);
        foreach (var previous in open.Where(x => x != same))
        {
            previous.ValidTo = now;
            previous.ClosedBy = employee;
        }
        if (open.Any(x => x != same)) await db.SaveChangesAsync(ct);
        if (!isVu && same is null)
            await db.ProductionSelectVersions.AddAsync(new ProductionSelectVersion {
                ProductionSelectVersionId = Guid.CreateVersion7(), CompanyId = order.CompanyId,
                MfgProductionOrderId = order.MfgProductionOrderId, ManufacturingFormulaId = source.FormulaId,
                CreatedBy = employee, ValidFrom = null
            }, ct);
        return OperationResult.Ok();
    }
}