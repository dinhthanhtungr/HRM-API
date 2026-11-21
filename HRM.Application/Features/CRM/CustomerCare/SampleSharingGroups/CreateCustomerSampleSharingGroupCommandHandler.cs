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
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

internal sealed class CreateCustomerSampleSharingGroupCommandHandler(ICustomerSampleSharingDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, IOptions<CustomerSampleSharingOptions> options)
    : IRequestHandler<CreateCustomerSampleSharingGroupCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(CreateCustomerSampleSharingGroupCommand command, CancellationToken ct)
    {
        if (!options.Value.Enabled || !user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } employee
            || company == Guid.Empty || employee == Guid.Empty || !permissions.HasPermission(ApplicationPermissions.CRM.ManageCustomerSampleSharing))
            return OperationResult<Guid>.Fail("Không có quyền quản lý nhóm dùng chung mẫu hoặc chức năng chưa bật.");
        var name = command.Request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return OperationResult<Guid>.Fail("Tên nhóm phải có từ 1 đến 150 ký tự.");
        await using var tx = await db.BeginCustomerSampleSharingTransactionAsync(ct);
        await db.LockCustomerSampleSharingAsync(company, ct);
        if (await db.CustomerSampleSharingGroups.AnyAsync(x => x.CompanyId == company && x.IsActive && x.Name.ToLower() == name.ToLower(), ct))
            return OperationResult<Guid>.Fail("Tên nhóm đã tồn tại.");
        var group = new CustomerSampleSharingGroup { CompanyId = company, Name = name, CreatedAt = clock.Now, CreatedBy = employee };
        db.CustomerSampleSharingGroups.Add(group);
        db.AuditLogs.Add(new AuditLog { CompanyId = company, SchemaName = "Customer", TableName = "customer_sample_sharing_groups",
            RecordId = group.Id, ActionType = AuditActionType.Create, ChangedBy = employee, ChangedAt = clock.Now,
            NewValues = JsonSerializer.SerializeToDocument(new { group.Name }), Reason = "Create customer sample sharing group" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult<Guid>.Ok(group.Id);
    }
}
