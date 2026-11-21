using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.LookupProcessingMaterials;

internal sealed class LookupProcessingMaterialsQueryHandler(IMaterialProcessingDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<LookupProcessingMaterialsQuery, IReadOnlyList<ProcessingMaterialOption>?>
{
    public async Task<IReadOnlyList<ProcessingMaterialOption>?> Handle(LookupProcessingMaterialsQuery request, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewMaterialProcessing) || user.CompanyId is null) return null;
        var watch = Stopwatch.StartNew();
        var previous = 0d;
        var timings = new List<string>();
        void Mark(string phase)
        {
            var now = watch.Elapsed.TotalMilliseconds;
            timings.Add($"mpf_{phase};dur={(now - previous).ToString("F2", CultureInfo.InvariantCulture)}");
            request.ServerTiming = string.Join(", ", timings);
            previous = now;
        }
        if (request.ProcessingType is not (null or "B" or "N" or "L")) return [];
        var maxCodeLength = request.ProcessingType is null ? 256 : 254;
        // Tổng hợp mã trùng một lần trong SQL, giữ cả NVL sai đơn vị/tên trong tập đối chiếu như trước.
        var duplicateCodes = db.Materials.AsNoTracking().Where(x => x.CompanyId == user.CompanyId
                && x.IsActive == true && x.Category.CompanyId == user.CompanyId
                && x.Category.IsActive == true && x.Category.Types == "NVL" && x.ExternalId != null)
            .GroupBy(x => x.ExternalId!.Trim().ToUpper())
            .Where(g => g.Count() > 1).Select(g => g.Key);
        var scoped = db.Materials.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && x.IsActive == true &&
            x.MaterialId != Guid.Empty && x.Unit != null && x.Unit.Trim().ToLower() == "kg" && x.ExternalId != null &&
            x.ExternalId.Trim() != "" && x.ExternalId.Length <= 256 && x.ExternalId.Trim().Length <= maxCodeLength &&
            (x.Name == null || x.Name.Length <= 1000) &&
            x.Category.CompanyId == user.CompanyId && x.Category.IsActive == true && x.Category.Types == "NVL" &&
            !duplicateCodes.Contains(x.ExternalId.Trim().ToUpper()));
        // Mã mới dùng dấu -, vẫn loại biến thể cũ dùng _ khỏi danh sách NVL gốc.
        scoped = scoped.Where(x => !x.ExternalId!.Trim().ToUpper().EndsWith("-B") &&
            !x.ExternalId.Trim().ToUpper().EndsWith("-N") && !x.ExternalId.Trim().ToUpper().EndsWith("-L"));
        if (request.ProcessingType is null)
        {
            var inputs = await scoped.Where(x => !x.ExternalId!.Trim().ToUpper().EndsWith("_B") &&
                !x.ExternalId.Trim().ToUpper().EndsWith("_N") && !x.ExternalId.Trim().ToUpper().EndsWith("_L"))
                .WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter))
                .OrderBy(x => x.ExternalId).ThenBy(x => x.MaterialId).Take(50)
                .Select(x => new ProcessingMaterialOption(x.MaterialId, x.ExternalId ?? "", x.Name ?? "",
                    null, null, null, null, false, x.CategoryId, x.Unit)).ToListAsync(ct);
            Mark("roots");
            return inputs;
        }
        var suffix = "-" + request.ProcessingType;
        var keyword = request.Keyword?.Trim() ?? "";
        var preferResin = request.ProcessingType is "B" or "N" && keyword.Length > 0 && keyword.All(c => c >= '0' && c <= '9');
        var preferredCode = "NVL_NH_" + keyword.PadLeft(3, '0');
        var roots = await scoped.Where(x => !System.Text.RegularExpressions.Regex.IsMatch(x.ExternalId!.Trim(), MaterialProcessingRules.ExistingSuffixPattern)).Where(x =>
                !x.ExternalId!.Trim().ToUpper().EndsWith("_B") &&
                !x.ExternalId.Trim().ToUpper().EndsWith("_N") &&
                !x.ExternalId.Trim().ToUpper().EndsWith("_L"))
            .WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.ExternalId ?? "") + suffix, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter))
            .OrderBy(x => preferResin && x.ExternalId!.Trim().ToUpper() == preferredCode ? 0
                : preferResin && EF.Functions.ILike(x.ExternalId!.Trim(), @"NVL\_NH\_%", "\\") ? 1 : 2)
            .ThenBy(x => x.ExternalId).ThenBy(x => x.MaterialId).Take(50)
            .Select(x => new HRM.Domain.Entities.MaterialSchema.Material {
                MaterialId = x.MaterialId, ExternalId = x.ExternalId, Name = x.Name,
                CategoryId = x.CategoryId, Unit = x.Unit, IsActive = x.IsActive
            }).ToListAsync(ct);
        Mark("roots");
        if (roots.Count == 0) return [];
        var codes = roots.Select(x => MaterialProcessingRules.OutputCode(x.ExternalId!, request.ProcessingType).ToUpperInvariant()).ToList();
        var variants = await db.Products.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && x.ColourCode != null &&
            codes.Contains(x.ColourCode.Trim().ToUpper()))
            .Select(x => new HRM.Domain.Entities.MaterialSchema.Material {
                MaterialId = x.ProductId, ExternalId = x.ColourCode, Name = x.Name,
                CategoryId = x.CategoryId, Unit = x.Unit, IsActive = x.IsActive && x.SampleRequests.Any(s => s.CompanyId == user.CompanyId
                    && s.IsActive && s.RequestType == SemiFinishedProcessing.RequestType && s.ExternalId == x.ColourCode)
            }).ToListAsync(ct);
        var materialCollisions = await db.Materials.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && x.ExternalId != null
                && codes.Contains(x.ExternalId.Trim().ToUpper()))
            .Select(x => new HRM.Domain.Entities.MaterialSchema.Material { MaterialId = x.MaterialId, ExternalId = x.ExternalId, IsActive = false }).ToListAsync(ct);
        variants.AddRange(materialCollisions);
        Mark("variants");
        var result = roots.Select(source => ProcessingLookupRules.Option(source, variants, request.ProcessingType))
            .Where(x => x is not null).Select(x => x!).ToList();
        Mark("map");
        return result;
    }
}

