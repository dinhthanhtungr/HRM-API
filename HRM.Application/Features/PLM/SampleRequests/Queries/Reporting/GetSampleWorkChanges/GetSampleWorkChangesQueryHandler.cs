using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.InternalMail.Queries.SampleWorkUnread;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleWorkChanges;

internal sealed class GetSampleWorkChangesQueryHandler(IPLMReadDbContext db, ICRMReadDbContext crm,
    ICustomerVisibilityService visibility, ICurrentUser user, SampleRequestLabTeamScope labTeams,
    ISender sender, SampleWorkPreviewSession? preview = null)
    : IRequestHandler<GetSampleWorkChangesQuery, IReadOnlyList<SampleWorkChangeDto>>
{
    public async Task<IReadOnlyList<SampleWorkChangeDto>> Handle(GetSampleWorkChangesQuery request, CancellationToken ct)
    {
        using var timings = HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.SampleWorkDiagnosticTags.Start("work_changes");
        if (request.SampleRequestIds is not { Length: > 0 and <= 100 } ||
            request.SampleRequestIds.Any(x => x == Guid.Empty) || !Enum.IsDefined(request.Audience) ||
            (request.LabTeam.HasValue && (!Enum.IsDefined(request.LabTeam.Value) || request.Audience == SampleRequestWorkAudience.Sale)))
            throw new ArgumentException("Invalid sample work change request.");
        if (!SampleRequestWorkQueueAccess.CanRead(user, request.Audience))
            throw new UnauthorizedAccessException("The current user cannot access this sample work queue.");
        var scope = await visibility.BuildReadScopeAsync(ct);
        if (request.Audience == SampleRequestWorkAudience.LabAdmin)
            scope = await SampleWorkPlanningVisibility.ForPlanningAsync(scope, user, db.Employees, ct);
        var ids = request.SampleRequestIds.Distinct().ToArray();
        timings?.Mark("visibility-scope");
        var rows = visibility.ApplySampleRequestVisibility(
            db.SampleRequests.AsNoTracking().Where(x => x.IsActive && ids.Contains(x.SampleRequestId)), db.Customers.AsNoTracking(), scope)
            .Where(x => x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        if (preview?.ActorReadScope is { } actorScope)
            rows = visibility.ApplySampleRequestVisibility(rows, db.Customers.AsNoTracking(), actorScope);
        if (request.Audience is SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin)
            rows = await labTeams.ApplyAsync(rows, request.LabTeam, ct);
        timings?.Mark("team-scope");

        // No work-category evaluation, priority sort, count, material/cost projection or full report.
        var revisions = await BuildRevisions(rows, db, scope.CompanyId).ToArrayAsync(ct);
        timings?.Mark("revisions");
        if (revisions.Length == 0) return [];
        var productIds = revisions.Select(x => x.ProductId).Distinct().ToArray();
        var prices = await crm.ProductPricingVersions.AsNoTracking()
            .Where(p => p.CompanyId == scope.CompanyId && p.IsActive && p.Status == HRM.Domain.Enums.CustomerEnum.ProductPricingStatus.Approved && productIds.Contains(p.ProductId))
            .GroupBy(p => p.ProductId).Select(g => new { ProductId = g.Key, Updated = g.Max(p => p.UpdatedDate ?? p.CreatedDate) })
            .ToDictionaryAsync(p => p.ProductId, p => p.Updated, ct);
        timings?.Mark("approved-prices");
        var unread = await sender.Send(new GetSampleWorkUnreadQuery(revisions.Select(x => x.SampleRequestId).ToArray()), ct);
        timings?.Mark("unread");
        return revisions.Select(x => new SampleWorkChangeDto(x.SampleRequestId,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Revision = x, Price = prices.GetValueOrDefault(x.ProductId) })))),
            unread?.GetValueOrDefault(x.SampleRequestId) ?? 0)).ToArray();
    }

    internal sealed record Revision(Guid SampleRequestId, Guid ProductId, DateTime? UpdatedDate, string? Status,
        DateTime? ProductUpdated, DateTime? FormulaUpdated, DateTime? EventUpdated, DateTime? TrialUpdated);

    internal static IQueryable<Revision> BuildRevisions(IQueryable<HRM.Domain.Entities.SampleRequestSchema.SampleRequest> rows,
        IPLMReadDbContext db, Guid company)
        => rows.Select(x => new Revision(
            x.SampleRequestId, x.ProductId, x.UpdatedDate, x.Status,
            x.Product.UpdatedDate,
            db.Formulas.Where(f => f.CompanyId == company && f.ProductId == x.ProductId)
                .Max(f => (DateTime?)(f.UpdatedDate ?? f.CreatedDate)),
            db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == company && e.SampleRequestId == x.SampleRequestId)
                .Max(e => (DateTime?)e.RecordedAt),
            db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId)
                .Max(t => (DateTime?)(t.UpdatedDate ?? t.CreatedDate))));
}
