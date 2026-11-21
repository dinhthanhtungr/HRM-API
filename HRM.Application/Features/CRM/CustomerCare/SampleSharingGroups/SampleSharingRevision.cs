using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

internal static class SampleSharingRevision
{
    // Opaque company-wide token: detects changes made by both legacy and batch writers,
    // without a new schema column or exposing out-of-scope membership.
    public static async Task<string> ReadAsync(ICustomerSampleSharingDbContext db, Guid company, CancellationToken ct)
    {
        var groups = await db.CustomerSampleSharingGroups.AsNoTracking().Where(x => x.CompanyId == company)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.IsActive }).ToArrayAsync(ct);
        var members = await db.CustomerSampleSharingMembers.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive)
            .OrderBy(x => x.CustomerId).ThenBy(x => x.Id).Select(x => new { x.Id, x.GroupId, x.CustomerId }).ToArrayAsync(ct);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { company, groups, members }))));
    }
}
