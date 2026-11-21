using HRM.Application.Abstractions.Persistence.PLM;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.CustomerLabels.Services;

/// <summary>Stages code-only label updates in the caller's product save; never changes Sale's detail rows.</summary>
public sealed class CustomerLabelColorCodeSyncService(IPLMWriteDbContext db)
{
    public async Task SyncAsync(Guid productId, Guid companyId, string? colorCode, CancellationToken ct)
    {
        var code = CustomerLabelRules.Normalize(colorCode);
        if (code is null) return;
        var labels = await db.CustomerLabelHeaders.Where(h => h.ProductId == productId &&
            h.Product.CompanyId == companyId && h.Customer.CompanyId == companyId).ToListAsync(ct);
        foreach (var label in labels)
        {
            if (label.ColorCode == code) continue;
            label.ColorCode = code;
            // Preserve Sale's edit timestamp/order and draft baseline. Product audit records the code change.
        }
    }
}

