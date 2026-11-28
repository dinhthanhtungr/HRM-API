using HRM.Application.Abstractions.Persistence.PLM;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

internal static class TdsSourceCompany
{
    public static async Task<Guid?> ResolveAsync(IPLMReadDbContext db, Guid? configured, CancellationToken ct)
    {
        if (configured.HasValue) return configured;
        // Count companies system-wide, including inactive ones, before assigning a shared folder.
        var ids = await db.Companies.Select(x => x.CompanyId).Take(2).ToListAsync(ct);
        return ids.Count == 1 ? ids[0] : null;
    }
}
