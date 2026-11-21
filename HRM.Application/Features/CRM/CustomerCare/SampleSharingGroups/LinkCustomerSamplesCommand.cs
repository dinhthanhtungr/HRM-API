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

public sealed record LinkCustomerSamplesCommand(Guid CustomerId, LinkSampleSharingCustomersRequest Request) : IRequest<OperationResult>;

/// <summary>Liên kết hai khách và nhóm hiện tại trong một transaction; không tự gộp hai nhóm.</summary>
internal sealed class LinkCustomerSamplesCommandHandler(ICustomerSampleSharingDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, CustomerSampleSharingService sharing)
    : IRequestHandler<LinkCustomerSamplesCommand, OperationResult>
{
    public async Task<OperationResult> Handle(LinkCustomerSamplesCommand command, CancellationToken ct)
    {
        if (!sharing.Enabled || !user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty
            || user.EmployeeId is not { } employee || employee == Guid.Empty
            || !permissions.HasPermission(ApplicationPermissions.CRM.ManageCustomerSampleSharing))
            return OperationResult.Fail("Không có quyền liên kết mẫu hoặc chức năng chưa bật.");
        if (command.CustomerId == Guid.Empty || command.Request.OtherCustomerId == Guid.Empty || command.CustomerId == command.Request.OtherCustomerId)
            return OperationResult.Fail("Chọn hai khách khác nhau để liên kết.");
        await using var tx = await db.BeginCustomerSampleSharingTransactionAsync(ct);
        await db.LockCustomerSampleSharingAsync(company, ct);
        var companyManager = permissions.HasPermission(ApplicationPermissions.CRM.AdminCustomerSampleSharing);
        var allowed = await sharing.ManageableCustomerIdsAsync(company, companyManager, ct);
        Guid[] ids = [command.CustomerId, command.Request.OtherCustomerId];
        if (ids.Any(id => !allowed.Contains(id))) return OperationResult.Fail("Khách không thuộc phạm vi quản lý của bạn.");
        var existing = await db.CustomerSampleSharingMembers.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && ids.Contains(x.CustomerId))
            .ToListAsync(ct);
        var groupIds = existing.Select(x => x.GroupId).Distinct().ToArray();
        if (groupIds.Length > 1) return OperationResult.Fail("Hai khách đang ở hai nhóm khác nhau. Không tự động gộp nhóm.");
        if (existing.Select(x => x.CustomerId).Distinct().Count() == 2)
            return OperationResult.Fail("Hai khách đã có trong cùng nhóm liên kết.");
        CustomerSampleSharingGroup group;
        if (groupIds.Length == 1)
        {
            var found = await db.CustomerSampleSharingGroups.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == company && x.Id == groupIds[0] && x.IsActive, ct);
            if (found is null) return OperationResult.Fail("Nhóm không còn hoạt động. Vui lòng tải lại.");
            group = found;
            if (await db.CustomerSampleSharingMembers.AnyAsync(x => x.CompanyId == company && x.GroupId == group.Id && x.IsActive && !allowed.Contains(x.CustomerId), ct))
                return OperationResult.Fail("Nhóm có khách ngoài phạm vi quản lý. Vui lòng liên hệ President/Dev.");
        }
        else
        {
            var name = string.IsNullOrWhiteSpace(command.Request.Name) ? $"LK-{Guid.CreateVersion7():N}" : command.Request.Name.Trim();
            if (name.Length > 150) return OperationResult.Fail("Tên nhóm tối đa 150 ký tự.");
            if (await db.CustomerSampleSharingGroups.AnyAsync(x => x.CompanyId == company && x.IsActive && x.Name.ToLower() == name.ToLower(), ct))
                return OperationResult.Fail("Tên nhóm đã tồn tại. Chọn khách trong nhóm đó để liên kết.");
            group = new() { CompanyId = company, Name = name, CreatedAt = clock.Now, CreatedBy = employee };
            db.CustomerSampleSharingGroups.Add(group);
        }
        foreach (var id in ids.Where(id => existing.All(m => m.CustomerId != id)))
            db.CustomerSampleSharingMembers.Add(new() { CompanyId = company, CustomerId = id, GroupId = group.Id, CreatedAt = clock.Now, CreatedBy = employee });
        db.AuditLogs.Add(new AuditLog { CompanyId = company, SchemaName = "Customer", TableName = "customer_sample_sharing_members",
            RecordId = command.CustomerId, ActionType = AuditActionType.Update, ChangedBy = employee, ChangedAt = clock.Now,
            NewValues = JsonSerializer.SerializeToDocument(new { GroupId = group.Id, CustomerIds = ids }), Reason = "Link customer samples" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult.Ok();
    }
}
