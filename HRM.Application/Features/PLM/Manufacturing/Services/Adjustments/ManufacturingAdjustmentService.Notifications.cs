using System.Text.Json;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed partial class ManufacturingAdjustmentService
{
    private async Task NotifyAsync(ManufacturingFormulaAdjustment entity, CancellationToken ct)
    {
        var groupIds = ManufacturingAdjustmentRecipientGroups.All;
        var recipients = await db.MemberInGroups.AsNoTracking().Where(x => groupIds.Contains(x.GroupId) && x.IsActive && x.Profile.HasValue && x.ProfileNavigation != null
            && x.ProfileNavigation.CompanyId == Company
            && x.ProfileNavigation.IsActive == true)
            .Select(x => x.Profile!.Value)
            .Distinct()
            .ToListAsync(ct);
        var actorName = await db.Employees.AsNoTracking().Where(x => x.EmployeeId == Employee && x.CompanyId == Company)
            .Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? user.UserName;
        await notifications.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = Company,
            CreatedBy = Employee,
            CreatedByNameSnapshot = actorName,
            Topic = TopicNotifications.ManufacturingFormulaAdjustmentCreated,
            Severity = NotificationSeverity.Info,
            Title = "Phiếu chỉnh màu được cập nhật",
            Message = $"Phiếu chỉnh màu cho lệnh {entity.MfgProductionOrder.ExternalId} đã được cập nhật.",
            AggregateId = entity.ManufacturingFormulaAdjustmentId,
            AggregateCode = entity.MfgProductionOrder.ExternalId,
            Link = $"/plpu/mfgformulaadjustments/{entity.ManufacturingFormulaAdjustmentId}",
            TargetUserIds = recipients,
            PayloadJson = JsonSerializer.Serialize(new
            {
                manufacturingFormulaAdjustmentId = entity.ManufacturingFormulaAdjustmentId,
                mfgProductionOrderId = entity.MfgProductionOrderId,
                mfgProductionOrderExternalId = entity.MfgProductionOrder.ExternalId,
                manufacturingFormulaId = entity.ManufacturingFormulaId,
                notifyGroupId = ManufacturingAdjustmentRecipientGroups.All[0],
                batchCount = entity.Batches.Count(x => x.IsActive),
                createdBy = Employee,
                createdByName = actorName,
                createdDate = entity.CreatedDate
            })
        }, ct);
    }

}
