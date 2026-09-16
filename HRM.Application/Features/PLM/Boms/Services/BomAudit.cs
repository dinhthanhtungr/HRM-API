using System.Text.Json;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;

namespace HRM.Application.Features.PLM.Boms.Services;

internal static class BomAudit
{
    internal static AuditLog Create(
        Guid companyId,
        Guid employeeId,
        string tableName,
        Guid recordId,
        string action,
        object values,
        string? reason = null,
        AuditActionType actionType = AuditActionType.Update,
        string schemaName = "bom")
        => new()
        {
            CompanyId = companyId,
            SchemaName = schemaName,
            TableName = tableName,
            RecordId = recordId,
            ActionType = actionType,
            ChangedBy = employeeId,
            ChangedAt = DateTime.Now,
            ChangedValues = JsonSerializer.SerializeToDocument(new { action, values }),
            Reason = reason
        };
}
