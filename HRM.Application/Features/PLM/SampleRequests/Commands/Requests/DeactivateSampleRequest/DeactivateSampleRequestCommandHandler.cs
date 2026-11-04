using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.Services.Requests;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DeactivateSampleRequest;

internal sealed class DeactivateSampleRequestCommandHandler(
    IPLMWriteDbContext db,
    ICurrentUser currentUser,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock)
    : IRequestHandler<DeactivateSampleRequestCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(
        DeactivateSampleRequestCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated ||
            !permissions.HasPermission(ApplicationPermissions.PLM.DeactivateSampleRequests))
            return OperationResult<Guid>.Fail("You are not allowed to deactivate sample requests.");

        var companyId = currentUser.CompanyId.GetValueOrDefault();
        var employeeId = currentUser.EmployeeId.GetValueOrDefault();
        if (request.SampleRequestId == Guid.Empty || companyId == Guid.Empty || employeeId == Guid.Empty)
            return OperationResult<Guid>.Fail("Sample request or current employee context is invalid.");

        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId &&
                x.CompanyId == companyId && x.IsActive, cancellationToken))
            return OperationResult<Guid>.Fail("Current employee was not found in this company.");

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var now = clock.Now;
        // Guard nằm ngay trong UPDATE để không áp dụng quyết định từ dữ liệu FE hoặc lần đọc cũ.
        var changed = await SampleRequestDeactivationRules.Eligible(db.SampleRequests, companyId, employeeId)
            .Where(x => x.SampleRequestId == request.SampleRequestId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedBy, employeeId)
                .SetProperty(x => x.UpdatedDate, now), cancellationToken);

        if (changed != 1)
            return OperationResult<Guid>.Fail(
                "Sample request is outside your scope, inactive, already sent or completed. Reload before retrying.");

        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = companyId,
            SchemaName = "SampleRequests",
            TableName = "SampleRequests",
            RecordId = request.SampleRequestId,
            ChangedBy = employeeId,
            ChangedAt = now,
            ActionType = AuditActionType.Update,
            Reason = "SampleRequestDeactivated",
            OldValues = JsonSerializer.SerializeToDocument(new { IsActive = true }),
            NewValues = JsonSerializer.SerializeToDocument(new { IsActive = false })
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<Guid>.Ok(request.SampleRequestId);
    }
}
