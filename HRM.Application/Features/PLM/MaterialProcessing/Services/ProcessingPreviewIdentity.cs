using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

/// <summary>Opaque lookup references; persisted formula rows use the resolved ProductId.</summary>
internal static class ProcessingPreviewIdentity
{
    private static readonly Guid GrindingMask = new("d781d954-67b8-46d8-b392-dc898b590e0a");
    private static readonly Guid ChoppingMask = new("fb0d7a78-9716-4511-9ad8-4de0438bc972");
    public static Guid Id(Guid materialId, string process)
    {
        var bytes = materialId.ToByteArray();
        var mask = (process == "N" ? GrindingMask : ChoppingMask).ToByteArray();
        for (var i = 0; i < bytes.Length; i++) bytes[i] ^= mask[i];
        return new Guid(bytes);
    }

    public static async Task<Product?> ResolveAsync(IPLMWriteDbContext db, Guid reference, Guid company,
        Guid employee, DateTime now, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(x => x.ProductId == reference && x.CompanyId == company, ct)) return null;
        var candidates = new[] { (Id: Id(reference, "N"), Process: "N"), (Id: Id(reference, "B"), Process: "B") };
        var ids = candidates.Select(x => x.Id).ToArray();
        var roots = await db.Materials.Where(x => ids.Contains(x.MaterialId) && x.CompanyId == company && x.IsActive == true
            && x.Category.CompanyId == company && x.Category.IsActive == true && x.Category.Types == "NVL").ToListAsync(ct);
        if (roots.Count == 0) return null;
        if (employee == Guid.Empty) throw new InvalidOperationException("Current employee is required to prepare processing material.");
        if (roots.Count != 1) throw new InvalidOperationException("Ambiguous processing material reference.");
        var root = roots[0];
        var process = candidates.Single(x => x.Id == root.MaterialId).Process;
        if (ProcessingLookupRules.Option(root, [], process) is null || MaterialProcessingRules.BaseCode(root.ExternalId!) != root.ExternalId!.Trim())
            throw new InvalidOperationException("Invalid processing material source.");
        await db.LockPurchasingCatalogAsync(company, ct);
        var code = MaterialProcessingRules.OutputCode(root.ExternalId!, process);
        var pending = db.Products.Local.FirstOrDefault(x => x.CompanyId == company && x.ColourCode == code && x.IsActive
            && x.CategoryId == root.CategoryId && x.Unit == root.Unit
            && x.SampleRequests.Any(s => s.IsActive && s.CompanyId == company && s.RequestType == SemiFinishedProcessing.RequestType));
        if (pending is not null) return pending;
        var resolved = await ProcessingProductResolver.ResolveAsync(db, root.MaterialId, process, company, employee, now, ct);
        return resolved.Product ?? throw new InvalidOperationException("Cannot prepare processing material: " + resolved.Error);
    }

    public static async Task<Material?> ResolveMaterialAsync(IPLMWriteDbContext db, Guid reference, Guid company,
        Guid employee, DateTime now, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.ProductId == reference && x.CompanyId == company
            && x.IsActive && x.ColourCode != null
            && (x.ColourCode.Trim().ToUpper().EndsWith("-N") || x.ColourCode.Trim().ToUpper().EndsWith("-B"))
            && x.SampleRequests.Any(s => s.IsActive && s.CompanyId == company && s.RequestType == SemiFinishedProcessing.RequestType), ct)
            ?? await ResolveAsync(db, reference, company, employee, now, ct);
        if (product is null) return null;

        await db.LockPurchasingCatalogAsync(company, ct);
        var code = product.ColourCode!.Trim().ToUpperInvariant();
        var pending = db.Materials.Local.FirstOrDefault(x => x.CompanyId == company && x.ExternalId == product.ColourCode);
        if (pending is not null) return pending;
        var existing = await db.Materials.Where(x => x.CompanyId == company && x.ExternalId != null
            && x.ExternalId.Trim().ToUpper() == code).Take(2).ToListAsync(ct);
        if (existing.Count == 1 && existing[0].IsActive == true &&
            existing[0].CategoryId == product.CategoryId && existing[0].Unit == product.Unit)
            return existing[0];
        if (existing.Count != 0)
            throw new InvalidOperationException("Cannot prepare processing material: OutputCodeExists");
        var source = await ProcessingProductResolver.SourceAsync(db, product.ProductId, company, ct);
        if (source is null || employee == Guid.Empty)
            throw new InvalidOperationException("Cannot prepare processing material: InvalidSource");
        return ProcessingProductResolver.AddMaterial(db, source, product, employee, now);
    }
}
