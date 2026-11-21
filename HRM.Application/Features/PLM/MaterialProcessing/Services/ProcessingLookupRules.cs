using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Domain.Entities.MaterialSchema;
namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

public static class ProcessingLookupRules
{
    public static ProcessingMaterialOption? Option(Material source, IEnumerable<Material> variants, string process)
    {
        if (source.MaterialId == Guid.Empty || source.IsActive != true || !MaterialProcessingRules.IsRootCode(source.ExternalId) ||
            source.ExternalId.Length > 256 || source.ExternalId.Trim().Length > 254 || (source.Name?.Length ?? 0) > 1000 ||
            !string.Equals(source.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)) return null;
        var code = MaterialProcessingRules.OutputCode(source.ExternalId!, process);
        var matches = variants.Where(x => string.Equals(x.ExternalId?.Trim(), code, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count > 1) return null;
        var target = matches.SingleOrDefault();
        if (target is not null && (target.IsActive != true || target.CategoryId != source.CategoryId ||
            !string.Equals(target.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase))) return null;
        return new(target?.MaterialId ?? source.MaterialId, code,
            target?.Name ?? (source.Name ?? source.ExternalId) + " (" + MaterialProcessingRules.ProcessName(process) + ")",
            source.MaterialId, source.ExternalId, source.Name, process, target is null, source.CategoryId, source.Unit);
    }
}
