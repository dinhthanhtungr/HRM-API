using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;

internal static class FormulaConfirmationTargetResolver
{
    // The caller must supply the current user's visibility-filtered query.
    internal static IQueryable<SampleRequest> Candidates(IQueryable<SampleRequest> visible,
        Guid company, Guid product, Guid? explicitRequestId)
    {
        var candidates = visible.Where(x => x.CompanyId == company && x.ProductId == product &&
            x.IsActive && x.Status != "Cancelled" && x.Status != "LabDeclined");
        return explicitRequestId.HasValue
            ? candidates.Where(x => x.SampleRequestId == explicitRequestId.Value)
            : candidates;
    }

    internal static SampleRequest RequireSingle(IReadOnlyList<SampleRequest> candidates)
        => candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException("No accessible sample request matches this formula product. Open the correct sample request before confirming."),
            _ => throw new InvalidOperationException("Multiple sample requests match this formula product. Select the intended sample request before confirming.")
        };

    internal static async Task<SampleRequest> ResolveAsync(IQueryable<SampleRequest> visible,
        Guid company, Guid product, Guid? explicitRequestId, CancellationToken ct)
        => RequireSingle(await Candidates(visible, company, product, explicitRequestId).AsTracking().Take(2).ToListAsync(ct));
}
