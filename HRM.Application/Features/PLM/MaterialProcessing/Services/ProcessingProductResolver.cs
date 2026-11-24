using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

internal static class ProcessingProductResolver
{
    public static async Task<Material?> SourceAsync(IPLMWriteDbContext db, Guid id, Guid company, CancellationToken ct)
    {
        var material = await db.Materials.FirstOrDefaultAsync(x => x.MaterialId == id && x.CompanyId == company && x.IsActive == true, ct);
        string? code = material?.ExternalId;
        if (material is null)
            code = await db.Products.Where(x => x.ProductId == id && x.CompanyId == company && x.IsActive
                && x.SampleRequests.Any(s => s.CompanyId == company && s.IsActive && s.RequestType == SemiFinishedProcessing.RequestType))
                .Select(x => x.ColourCode).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(code)) return null;
        var baseCode = MaterialProcessingRules.BaseCode(code).ToUpperInvariant();
        if (!MaterialProcessingRules.IsRootCode(baseCode)) return null;
        var roots = await db.Materials.Where(x => x.CompanyId == company && x.IsActive == true
                && x.ExternalId != null && x.ExternalId.Trim().ToUpper() == baseCode && x.Category.CompanyId == company
                && x.Category.IsActive == true && x.Category.Types == "NVL")
            .Take(2).ToListAsync(ct);
        return roots.Count == 1 && string.Equals(roots[0].Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase) ? roots[0] : null;
    }

    public static async Task<(string? Error, Product? Product, Material? Source)> ResolveAsync(
        IPLMWriteDbContext db, Guid id, string process, Guid company, Guid employee, DateTime now, CancellationToken ct)
    {
        var source = await SourceAsync(db, id, company, ct);
        if (source is null) return ("InvalidSource", null, null);
        var code = MaterialProcessingRules.OutputCode(source.ExternalId!, process);
        var products = await db.Products.Where(x => x.CompanyId == company && x.ColourCode != null && x.ColourCode.Trim().ToUpper() == code.ToUpper())
            .Include(x => x.SampleRequests).Take(2).ToListAsync(ct);
        if (products.Count > 0)
        {
            var product = products[0];
            var samples = product.SampleRequests.Where(s => s.CompanyId == company && s.IsActive && s.RequestType == SemiFinishedProcessing.RequestType && s.ExternalId == code).ToList();
            if (products.Count != 1 || !product.IsActive || product.CategoryId != source.CategoryId || product.Unit != source.Unit || samples.Count != 1)
                return ("OutputCodeExists", null, null);
            if (!await ProcessingDefaultFormula.EnsureAsync(db, product, samples[0], source, process, employee, now, ct))
                return ("OutputCodeExists", null, null);
            if (MaterialProcessingRules.InternalColourName(process) is { } colour && product.ColourName != colour)
            {
                product.ColourName = colour;
                product.UpdatedBy = employee;
                product.UpdatedDate = now;
            }
            return (null, product, source);
        }
        if (code.Length > 256 || await db.SampleRequests.AnyAsync(x => x.CompanyId == company && x.ExternalId.ToUpper() == code.ToUpper(), ct)
            || await db.Materials.AnyAsync(x => x.CompanyId == company && x.ExternalId != null && x.ExternalId.Trim().ToUpper() == code.ToUpper(), ct))
            return ("OutputCodeExists", null, null);
        var customer = await db.Customers.Where(x => x.CompanyId == company && x.IsActive == true && x.ExternalId == PLMCustomerRules.InternalCustomerExternalId)
            .Select(x => (Guid?)x.CustomerId).FirstOrDefaultAsync(ct);
        if (customer is null) return ("InternalCustomerRequired", null, null);
        var productNew = new Product { ProductId = Guid.CreateVersion7(), ColourCode = code, Code = source.CustomCode, LabComment = source.Comment,
            ColourName = MaterialProcessingRules.InternalColourName(process),
            Name = (source.Name ?? source.ExternalId) + " (" + MaterialProcessingRules.ProcessName(process) + ")",
            CategoryId = source.CategoryId, Unit = source.Unit, Weight = source.Weight,
            CompanyId = company, IsActive = true, CreatedBy = employee, UpdatedBy = employee, CreatedDate = now, UpdatedDate = now };
        var attachments = new AttachmentCollection { AttachmentCollectionId = Guid.CreateVersion7() };
        var sample = new SampleRequest { SampleRequestId = Guid.CreateVersion7(), ExternalId = code, ProductId = productNew.ProductId,
            Product = productNew, RequestType = SemiFinishedProcessing.RequestType, Status = "Completed",
            CustomerId = customer.Value, ManagerBy = employee, BranchId = company, CompanyId = company,
            AttachmentCollectionId = attachments.AttachmentCollectionId, AttachmentCollection = attachments,
            Package = source.Package ?? "", IsActive = true, CreatedBy = employee, UpdatedBy = employee, CreatedDate = now, UpdatedDate = now };
        db.Products.Add(productNew);
        db.SampleRequests.Add(sample);
        if (!await ProcessingDefaultFormula.EnsureAsync(db, productNew, sample, source, process, employee, now, ct))
            return ("OutputCodeExists", null, null);
        if (process is "N" or "B")
            AddMaterial(db, source, productNew, employee, now);
        return (null, productNew, source);
    }

    internal static Material AddMaterial(IPLMWriteDbContext db, Material source, Product product,
        Guid employee, DateTime now)
    {
        var material = new Material
        {
            MaterialId = Guid.CreateVersion7(),
            ExternalId = product.ColourCode,
            Name = product.Name,
            CustomCode = source.CustomCode,
            CategoryId = source.CategoryId,
            Weight = source.Weight,
            Unit = source.Unit,
            Package = source.Package,
            Comment = source.Comment,
            MinQuantity = source.MinQuantity,
            CompanyId = source.CompanyId,
            IsActive = source.IsActive,
            AttachmentCollectionId = source.AttachmentCollectionId,
            CreatedBy = employee,
            CreatedDate = now,
            UpdatedBy = employee,
            UpdatedDate = now
        };
        db.Materials.Add(material);
        return material;
    }
}
