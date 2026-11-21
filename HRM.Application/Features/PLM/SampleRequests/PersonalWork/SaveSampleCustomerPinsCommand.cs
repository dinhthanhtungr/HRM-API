using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Work;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.PersonalWork;

public sealed record SaveSampleCustomerPinsRequest(
    [property: JsonRequired] bool IsPinned,
    [property: JsonRequired, JsonConverter(typeof(JsonStringEnumConverter<SampleRequestWorkAudience>))] SampleRequestWorkAudience Audience);
public sealed record SampleCustomerPinDto(Guid SampleRequestId, bool IsPinned, DateTime? PinnedAt);
public sealed record SaveSampleCustomerPinsCommand(Guid CustomerId, SaveSampleCustomerPinsRequest Body)
    : IRequest<OperationResult<IReadOnlyList<SampleCustomerPinDto>>>;

/// <summary>Ghim riêng toàn bộ TP của khách trong phạm vi hàng đợi; không tải daily, note hoặc giá.</summary>
internal sealed class SaveSampleCustomerPinsCommandHandler(IPLMWriteDbContext db, IWorkTaskWriteDbContext work,
    ICurrentUser user, ICustomerVisibilityService visibility, SampleRequestLabTeamScope labTeams)
    : IRequestHandler<SaveSampleCustomerPinsCommand, OperationResult<IReadOnlyList<SampleCustomerPinDto>>>
{
    public async Task<OperationResult<IReadOnlyList<SampleCustomerPinDto>>> Handle(SaveSampleCustomerPinsCommand command, CancellationToken ct)
    {
        if (!SamplePersonalWorkAccess.HasActor(user) || command.CustomerId == Guid.Empty ||
            !Enum.IsDefined(command.Body.Audience) || !SampleRequestWorkQueueAccess.CanRead(user, command.Body.Audience))
            return Fail();
        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct))
            return Fail();
        var scope = await visibility.BuildReadScopeAsync(ct);
        if (command.Body.Audience == SampleRequestWorkAudience.LabAdmin)
            scope = await SampleWorkPlanningVisibility.ForPlanningAsync(scope, user, db.Employees, ct);
        var readable = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope);
        readable = CustomerRequests(readable, company, command.CustomerId);
        if (command.Body.Audience is SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin)
            readable = await labTeams.ApplyAsync(readable, null, ct);
        var ids = await readable.Select(x => x.SampleRequestId).OrderBy(x => x).ToArrayAsync(ct);
        if (ids.Length == 0) return OperationResult<IReadOnlyList<SampleCustomerPinDto>>.Ok([]);
        var now = FormulaConcurrencyRules.NormalizeDatabaseTimestamp(DateTime.Now);
        await using var transaction = await db.BeginTransactionAsync(ct);
        // Same parent locks as single-TP saves, in stable order to avoid deadlocks.
        await work.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"SampleRequests\".\"SampleRequests\" WHERE \"CompanyId\" = {company} AND \"SampleRequestId\" = ANY({ids}) ORDER BY \"SampleRequestId\" FOR UPDATE", ct);
        await work.Database.ExecuteSqlInterpolatedAsync(Upsert(ids, company, employee, command.Body.IsPinned, now), ct);
        var result = await db.SampleRequestUserPreferences.AsNoTracking()
            .Where(x => x.CompanyId == company && x.EmployeeId == employee && ids.Contains(x.SampleRequestId))
            .Select(x => new SampleCustomerPinDto(x.SampleRequestId, x.IsPinned, x.PinnedAt)).ToArrayAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<IReadOnlyList<SampleCustomerPinDto>>.Ok(result);
    }

    internal static IQueryable<HRM.Domain.Entities.SampleRequestSchema.SampleRequest> CustomerRequests(
        IQueryable<HRM.Domain.Entities.SampleRequestSchema.SampleRequest> query, Guid company, Guid customer)
        => query.Where(x => x.CompanyId == company && x.IsActive && x.CustomerId == customer &&
            (x.CreatedDate >= SampleFlowRules.StartsAt || x.WorkflowEvents.Any(e => e.CompanyId == x.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) &&
            x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
            x.Customer.ExternalId != HRM.Application.Features.PLM.Shared.Rules.PLMCustomerRules.InternalCustomerExternalId);

    internal static FormattableString Upsert(Guid[] ids, Guid company, Guid employee, bool pinned, DateTime now)
    {
        var keys = ids.Select(_ => Guid.CreateVersion7()).ToArray();
        // Raw SQL has no EF property mapping: DateTime defaults to timestamptz,
        // which rejects Local clocks. Bind invariant text and cast to the column's
        // timestamp-without-zone type, preserving the existing local business clock.
        var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.ffffff", System.Globalization.CultureInfo.InvariantCulture);
        return $"""
            INSERT INTO "SampleRequests"."SampleRequestUserPreferences"
                ("Id", "CompanyId", "EmployeeId", "SampleRequestId", "IsPinned", "NotifyOnDue", "PinnedAt", "CreatedDate", "UpdatedDate")
            SELECT source.id, {company}, {employee}, source.request_id, {pinned}, false,
                CASE WHEN {pinned} THEN CAST({timestamp} AS timestamp without time zone) ELSE NULL END,
                CAST({timestamp} AS timestamp without time zone), CAST({timestamp} AS timestamp without time zone)
            FROM unnest({keys}, {ids}) AS source(id, request_id)
            ON CONFLICT ("CompanyId", "EmployeeId", "SampleRequestId") DO UPDATE
            SET "IsPinned" = EXCLUDED."IsPinned",
                "PinnedAt" = CASE WHEN EXCLUDED."IsPinned" THEN COALESCE("SampleRequestUserPreferences"."PinnedAt", EXCLUDED."PinnedAt") ELSE NULL END,
                "UpdatedDate" = EXCLUDED."UpdatedDate"
            """;
    }

    private static OperationResult<IReadOnlyList<SampleCustomerPinDto>> Fail()
        => OperationResult<IReadOnlyList<SampleCustomerPinDto>>.Fail("Customer pin request is not accessible.");
}
