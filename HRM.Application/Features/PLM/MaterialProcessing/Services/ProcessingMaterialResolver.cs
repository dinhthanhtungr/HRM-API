using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

public sealed class ProcessingMaterialResolver(IMaterialProcessingDbContext db)
{
    public async Task<List<Material>?> InputsAsync(IReadOnlyList<HRM.Application.Features.PLM.MaterialProcessing.Dtos.MaterialProcessingLineRequest> lines,
        Guid company, CancellationToken ct)
    {
        var ids = lines.Select(x => x.MaterialId).Distinct().ToArray();
        var selected = await db.Materials.AsNoTracking().Where(x => ids.Contains(x.MaterialId) && x.CompanyId == company && x.IsActive == true).ToListAsync(ct);
        if (selected.Count != ids.Length) return null;
        var codes = selected.Select(x => MaterialProcessingRules.BaseCode(x.ExternalId ?? "").ToUpperInvariant()).Distinct().ToArray();
        var roots = await db.Materials.Where(x => x.CompanyId == company && x.IsActive == true && x.ExternalId != null
            && codes.Contains(x.ExternalId.Trim().ToUpper()) && x.Category.CompanyId == company && x.Category.IsActive == true && x.Category.Types == "NVL")
            .ToListAsync(ct);
        var result = new List<Material>();
        foreach (var line in lines)
        {
            var code = MaterialProcessingRules.BaseCode(selected.Single(x => x.MaterialId == line.MaterialId).ExternalId ?? "");
            var matches = roots.Where(x => string.Equals(x.ExternalId?.Trim(), code, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 || !string.Equals(matches[0].Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)
                || (matches[0].ExternalId?.Length ?? 0) > 256 || (matches[0].Name?.Length ?? 0) > 1000) return null;
            result.Add(matches[0]);
        }
        return result;
    }
    public async Task<Material?> BaseAsync(Material selected, Guid company, CancellationToken ct)
    {
        var baseCode = MaterialProcessingRules.BaseCode(selected.ExternalId ?? "");
        if (baseCode == selected.ExternalId?.Trim()) return selected;
        var matches = await db.Materials.Where(x => x.CompanyId == company && x.IsActive == true &&
            x.ExternalId != null && x.ExternalId.Trim().ToUpper() == baseCode.ToUpper()).Take(2).ToListAsync(ct);
        return matches.Count == 1 ? matches[0] : null;
    }

    // Sao chép mọi cột thông tin của Material; không sao chép navigation/lịch sử giao dịch.
    public static Material Clone(Material source, string code, string processName, Guid employee, DateTime now)
        => new()
        {
            MaterialId = Guid.CreateVersion7(), ExternalId = code, Name = (source.Name ?? source.ExternalId) + " (" + processName + ")",
            CustomCode = source.CustomCode, CategoryId = source.CategoryId, Weight = source.Weight,
            Unit = source.Unit, Package = source.Package, Comment = source.Comment, MinQuantity = source.MinQuantity,
            CompanyId = source.CompanyId, IsActive = source.IsActive, Barcode = source.Barcode,
            AttachmentCollectionId = source.AttachmentCollectionId,
            CreatedBy = employee, UpdatedBy = employee, CreatedDate = now, UpdatedDate = now
        };
}
