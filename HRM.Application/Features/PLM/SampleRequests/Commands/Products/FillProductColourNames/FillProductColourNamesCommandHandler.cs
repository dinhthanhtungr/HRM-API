using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.FillProductColourNames;

internal sealed class FillProductColourNamesCommandHandler(IPLMWriteDbContext db, ICurrentUser user, IDateTimeProvider clock)
    : IRequestHandler<FillProductColourNamesCommand, OperationResult<FillProductColourNamesResult>>
{
    public async Task<OperationResult<FillProductColourNamesResult>> Handle(FillProductColourNamesCommand request, CancellationToken ct)
    {
        if (request.ExportPreview && !request.DryRun)
            return OperationResult<FillProductColourNamesResult>.Fail("Excel export is only available for dryRun=true.");
        if (!user.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors) ||
            user.CompanyId is not { } company || user.EmployeeId is not { } employee || employee == Guid.Empty)
            return OperationResult<FillProductColourNamesResult>.Fail("Product technical edit permission and current company/employee are required.");

        var source = db.Products.AsNoTracking().Where(p => p.CompanyId == company && p.IsActive && p.ColourName == null &&
            p.Category != null && p.Category.CompanyId == company && p.Category.IsActive == true &&
            (p.Category.ExternalId == "CMB" || p.Category.ExternalId == "PIG"));
        var examined = 0; var matched = 0; var updated = 0; var conflicts = 0;
        var preview = new List<ProductColourNameCandidate>();
        Guid? cursor = null;
        var correlation = Guid.CreateVersion7();
        await using var transaction = request.DryRun ? null : await db.BeginTransactionAsync(ct);
        while (true)
        {
            var page = await source.Where(p => !cursor.HasValue || p.ProductId.CompareTo(cursor.Value) > 0)
                .OrderBy(p => p.ProductId).Take(250).ToArrayAsync(ct);
            if (page.Length == 0) break;
            foreach (var product in page)
            {
                examined++;
                var colour = ProductColourNameMatcher.Match(product.Name);
                if (request.ExportPreview || preview.Count < 100)
                    preview.Add(new(product.ProductId, product.Name, colour, product.ColourCode));
                if (colour is null) continue;
                matched++;
                if (request.DryRun) continue;
                var now = clock.Now;
                var affected = await source.Where(p => p.ProductId == product.ProductId && p.Name == product.Name)
                    .ExecuteUpdateAsync(set => set.SetProperty(p => p.ColourName, colour)
                        .SetProperty(p => p.UpdatedDate, now).SetProperty(p => p.UpdatedBy, employee), ct);
                if (affected == 0) { conflicts++; continue; }
                var before = SampleRequestDataChangeAuditHelper.BuildProductAuditSnapshot(product);
                product.ColourName = colour;
                await SampleRequestDataChangeAuditHelper.AddProductAuditIfChangedAsync(db, product, employee, now,
                    correlation, before, "ProductColourNameFilledFromName", ct);
                updated++;
            }
            if (!request.DryRun) await db.SaveChangesAsync(ct);
            cursor = page[^1].ProductId;
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return OperationResult<FillProductColourNamesResult>.Ok(new FillProductColourNamesResult(request.DryRun, examined, matched, updated, conflicts, preview, examined > preview.Count));
    }
}
