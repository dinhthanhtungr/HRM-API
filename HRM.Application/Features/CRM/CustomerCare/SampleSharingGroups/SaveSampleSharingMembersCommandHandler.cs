using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Audits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

internal sealed class SaveSampleSharingMembersCommandHandler(ICustomerSampleSharingDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, CustomerSampleSharingService sharing)
    : IRequestHandler<SaveSampleSharingMembersCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(SaveSampleSharingMembersCommand command, CancellationToken ct)
    {
        var r = command.Request;
        if (!sharing.Enabled || !user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty
            || user.EmployeeId is not { } employee || employee == Guid.Empty
            || !permissions.HasPermission(ApplicationPermissions.CRM.ManageCustomerSampleSharing))
            return OperationResult<Guid>.Fail("Không có quyền quản lý liên kết hoặc chức năng chưa bật.");
        var admin = permissions.HasPermission(ApplicationPermissions.CRM.AdminCustomerSampleSharing);
        if (r is null || r.CustomerIds is null || r.CustomerIds.Count > 500 || r.CustomerIds.Any(x => x == Guid.Empty)
            || r.CustomerIds.Distinct().Count() != r.CustomerIds.Count || r.GroupId == Guid.Empty || r.AnchorCustomerId == Guid.Empty
            || string.IsNullOrWhiteSpace(r.Revision) || r.Name?.Trim().Length > 150
            || (!admin && r.AnchorCustomerId is null))
            return OperationResult<Guid>.Fail("Dữ liệu liên kết không hợp lệ.");
        await using var tx = await db.BeginCustomerSampleSharingTransactionAsync(ct);
        await db.LockCustomerSampleSharingAsync(company, ct);
        if (r.Revision != await SampleSharingRevision.ReadAsync(db, company, ct))
            return OperationResult<Guid>.Fail("Liên kết đã thay đổi. Tải lại và đối chiếu bản nháp trước khi lưu.");
        var allowed = await sharing.ManageableCustomerIdsAsync(company, admin, ct);
        if (r.CustomerIds.Any(x => !allowed.Contains(x)) || (r.AnchorCustomerId is { } anchor && !allowed.Contains(anchor)))
            return OperationResult<Guid>.Fail("Khách không thuộc phạm vi quản lý của bạn.");
        var group = r.GroupId is { } id ? await db.CustomerSampleSharingGroups.AsTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == company && x.Id == id && x.IsActive, ct) : null;
        if (r.GroupId.HasValue && group is null) return OperationResult<Guid>.Fail("Nhóm không còn hoạt động.");
        var memberships = await db.CustomerSampleSharingMembers.AsTracking().Where(x => x.CompanyId == company && x.IsActive).ToArrayAsync(ct);
        if (!admin && r.AnchorCustomerId is { } current)
        {
            var anchorGroup = memberships.FirstOrDefault(x => x.CustomerId == current)?.GroupId;
            if (anchorGroup != r.GroupId || (r.GroupId is null && !r.CustomerIds.Contains(current)))
                return OperationResult<Guid>.Fail("Nhóm đích không khớp khách đang làm việc. Vui lòng tải lại.");
        }
        var affected = memberships.Where(x => x.GroupId == r.GroupId || r.CustomerIds.Contains(x.CustomerId)).Select(x => x.GroupId).Distinct().ToArray();
        if (await db.CustomerSampleSharingGroups.CountAsync(x => x.CompanyId == company && x.IsActive && affected.Contains(x.Id), ct) != affected.Length)
            return OperationResult<Guid>.Fail("Nhóm nguồn hoặc đích không còn hoạt động. Vui lòng tải lại.");
        if (memberships.Any(x => affected.Contains(x.GroupId) && !allowed.Contains(x.CustomerId)))
            return OperationResult<Guid>.Fail("Nhóm nguồn hoặc đích có khách ngoài phạm vi quản lý.");
        // User-facing links have no name input. Preserve existing internal names;
        // create a unique internal label when the caller omits it.
        var name = string.IsNullOrWhiteSpace(r.Name) ? group?.Name ?? $"LK-{Guid.CreateVersion7():N}" : r.Name.Trim();
        if (await db.CustomerSampleSharingGroups.AnyAsync(x => x.CompanyId == company && x.IsActive && x.Id != r.GroupId && x.Name.ToLower() == name.ToLower(), ct))
            return OperationResult<Guid>.Fail("Tên nhóm đã tồn tại.");
        if (group is null)
        {
            group = new() { CompanyId = company, Name = name, CreatedAt = clock.Now, CreatedBy = employee };
            db.CustomerSampleSharingGroups.Add(group);
        }
        var before = memberships.Where(x => affected.Contains(x.GroupId)).Select(x => new { x.CustomerId, x.GroupId }).ToArray();
        group.Name = name;
        foreach (var row in memberships.Where(x => (x.GroupId == group.Id && !r.CustomerIds.Contains(x.CustomerId))
                     || (x.GroupId != group.Id && r.CustomerIds.Contains(x.CustomerId))))
        { row.IsActive = false; row.RemovedAt = clock.Now; row.RemovedBy = employee; }
        // Flush deactivations first for the partial unique index. Both saves are inside one transaction.
        await db.SaveChangesAsync(ct);
        foreach (var customer in r.CustomerIds.Where(x => !memberships.Any(m => m.CustomerId == x && m.GroupId == group.Id && m.IsActive)))
            db.CustomerSampleSharingMembers.Add(new() { CompanyId = company, GroupId = group.Id, CustomerId = customer, CreatedAt = clock.Now, CreatedBy = employee });
        db.AuditLogs.Add(new AuditLog { CompanyId = company, SchemaName = "Customer", TableName = "customer_sample_sharing_members",
            RecordId = group.Id, ActionType = AuditActionType.Update, ChangedBy = employee, ChangedAt = clock.Now,
            OldValues = JsonSerializer.SerializeToDocument(before), NewValues = JsonSerializer.SerializeToDocument(new { group.Name, r.CustomerIds }),
            Reason = "Save customer sample sharing membership batch" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult<Guid>.Ok(group.Id);
    }
}
