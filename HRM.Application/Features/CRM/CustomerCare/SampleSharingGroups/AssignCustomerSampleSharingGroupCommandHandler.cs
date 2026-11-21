using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Rules;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Audits;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

internal sealed class AssignCustomerSampleSharingGroupCommandHandler(ICustomerSampleSharingDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, IOptions<CustomerSampleSharingOptions> options,
    CustomerSampleSharingService sharing)
    : IRequestHandler<AssignCustomerSampleSharingGroupCommand, OperationResult>
{
    public async Task<OperationResult> Handle(AssignCustomerSampleSharingGroupCommand command, CancellationToken ct)
    {
        if (!options.Value.Enabled || !user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } employee
            || company == Guid.Empty || employee == Guid.Empty || !permissions.HasPermission(ApplicationPermissions.CRM.ManageCustomerSampleSharing))
            return OperationResult.Fail("Không có quyền quản lý nhóm dùng chung mẫu hoặc chức năng chưa bật.");
        if (command.CustomerId == Guid.Empty || command.Request.GroupId == Guid.Empty)
            return OperationResult.Fail("Mã khách hoặc nhóm không hợp lệ.");
        await using var tx = await db.BeginCustomerSampleSharingTransactionAsync(ct);
        await db.LockCustomerSampleSharingAsync(company, ct);
        var companyManager = user.IsInAnyRole(ApplicationRoleSets.CRM.SampleSharingCompanyManagers);
        var manageable = await sharing.ManageableCustomerIdsAsync(company, companyManager, ct);
        if (!manageable.Contains(command.CustomerId))
            return OperationResult.Fail("Khách không thuộc phạm vi quản lý của bạn.");
        var relatedGroups = db.CustomerSampleSharingMembers.Where(x => x.CompanyId == company && x.IsActive
            && x.CustomerId == command.CustomerId).Select(x => x.GroupId);
        if (!companyManager && await db.CustomerSampleSharingMembers.AnyAsync(x => x.CompanyId == company && x.IsActive
            && (relatedGroups.Contains(x.GroupId) || x.GroupId == command.Request.GroupId) && !manageable.Contains(x.CustomerId), ct))
            return OperationResult.Fail("Nhóm có khách ngoài phạm vi quản lý. Vui lòng liên hệ President/Dev.");
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == command.CustomerId
            && x.CompanyId == company && x.IsActive == true, ct);
        if (customer is null || InternalCustomerRules.IsInternalCustomerExternalId(customer.ExternalId))
            return OperationResult.Fail("Khách không hợp lệ trong công ty hoặc là khách nội bộ.");
        if (command.Request.GroupId is { } groupId && !await db.CustomerSampleSharingGroups.AnyAsync(x => x.Id == groupId
            && x.CompanyId == company && x.IsActive, ct))
            return OperationResult.Fail("Nhóm không hoạt động hoặc không thuộc công ty.");
        var old = await db.CustomerSampleSharingMembers.Where(x => x.CompanyId == company && x.CustomerId == command.CustomerId
            && x.IsActive).AsTracking().ToListAsync(ct);
        if (old.Count == 1 && old[0].GroupId == command.Request.GroupId) return OperationResult.Ok();
        foreach (var row in old) { row.IsActive = false; row.RemovedAt = clock.Now; row.RemovedBy = employee; }
        // Persist deactivation first because PostgreSQL checks the active-membership unique index per statement.
        if (old.Count > 0) await db.SaveChangesAsync(ct);
        if (command.Request.GroupId is { } target)
            db.CustomerSampleSharingMembers.Add(new CustomerSampleSharingMember { CompanyId = company, GroupId = target,
                CustomerId = command.CustomerId, CreatedAt = clock.Now, CreatedBy = employee });
        db.AuditLogs.Add(new AuditLog { CompanyId = company, SchemaName = "Customer", TableName = "customer_sample_sharing_members",
            RecordId = command.CustomerId, ActionType = AuditActionType.Update, ChangedBy = employee, ChangedAt = clock.Now,
            OldValues = JsonSerializer.SerializeToDocument(old.Select(x => x.GroupId).ToArray()),
            NewValues = JsonSerializer.SerializeToDocument(new { command.Request.GroupId }), Reason = "Change customer sample sharing group" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult.Ok();
    }
}
