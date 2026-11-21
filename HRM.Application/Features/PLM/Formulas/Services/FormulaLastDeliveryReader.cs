using HRM.Application.Abstractions.Persistence.PLM;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Services;

internal static class FormulaLastDeliveryReader
{
    internal sealed class DeliveryRow
    {
        public Guid FormulaId { get; init; }
        public string? Kind { get; init; }
    }

    internal static IQueryable<DeliveryRow> Query(IPLMDbContext db, Guid company, IReadOnlyCollection<Guid> formulaIds)
        => db.Formulas.AsNoTracking().Where(f => f.CompanyId == company && f.IsActive && formulaIds.Contains(f.FormulaId))
            .Select(f => new DeliveryRow {
                FormulaId = f.FormulaId,
                Kind = db.SampleRequestSampleTrials.Where(t => t.FormulaId == f.FormulaId && t.IsActive &&
                    t.SentDate != null && t.SampleRequest.IsActive && t.SampleRequest.CompanyId == company)
                    .OrderByDescending(t => t.SentDate).ThenByDescending(t => t.TrialNo).ThenByDescending(t => t.SampleRequestSampleTrialId)
                    .Select(t => t.DeliveredSampleQuantityKg == 0 ? "ColorChip" : t.DeliveredSampleQuantityKg > 0 ? "TrialSample" : "Unknown")
                    .FirstOrDefault()
            });

    internal static async Task<Dictionary<Guid, string>> ReadAsync(IPLMDbContext db, Guid company,
        IReadOnlyCollection<Guid> formulaIds, CancellationToken ct)
    {
        if (formulaIds.Count == 0) return [];
        var rows = await Query(db, company, formulaIds).ToListAsync(ct);
        return rows.Where(r => r.Kind is not null).ToDictionary(r => r.FormulaId, r => r.Kind!);
    }
}
