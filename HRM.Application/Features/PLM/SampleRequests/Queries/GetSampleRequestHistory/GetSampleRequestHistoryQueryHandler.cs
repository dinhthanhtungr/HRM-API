using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.History;
using HRM.Domain.Entities.AuditSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestHistory;

internal sealed class GetSampleRequestHistoryQueryHandler
    : IRequestHandler<GetSampleRequestHistoryQuery, SampleRequestHistoryResponseDto?>
{
    private const string SampleRequestsSchema = "SampleRequests";
    private const string SampleRequestsTable = "SampleRequests";
    private const string ProductsTable = "Products";
    private const string AttachmentsTable = "Attachments";

    private static readonly IReadOnlySet<string> RestrictedProductHistoryFields = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "ColourName",
        "Additive",
    };

    private readonly IPLMReadDbContext _dbContext;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetSampleRequestHistoryQueryHandler(
        IPLMReadDbContext dbContext,
        IPLMFieldVisibilityService fieldVisibility,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _fieldVisibility = fieldVisibility;
        _visibilityService = visibilityService;
    }

    public async Task<SampleRequestHistoryResponseDto?> Handle(
        GetSampleRequestHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return null;
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var sampleRequestQuery = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests
                .Where(x => x.SampleRequestId == request.SampleRequestId)
                .AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        var sampleRequest = await sampleRequestQuery
            .Select(x => new
            {
                x.SampleRequestId,
                x.ProductId,
                x.CompanyId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return null;
        }

        var auditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.SchemaName == SampleRequestsSchema
                && (x.CompanyId == sampleRequest.CompanyId || x.CompanyId == null)
                && ((x.TableName == SampleRequestsTable && x.RecordId == sampleRequest.SampleRequestId)
                    || (x.TableName == ProductsTable && x.RecordId == sampleRequest.ProductId)
                    || (x.TableName == AttachmentsTable && x.RecordId == sampleRequest.SampleRequestId)))
            .OrderByDescending(x => x.ChangedAt)
            .ThenByDescending(x => x.AuditLogId)
            .ToListAsync(cancellationToken);

        var changedByIds = auditLogs
            .Select(x => x.ChangedBy)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var employeeNamesById = changedByIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Employees
                .AsNoTracking()
                .Where(x => changedByIds.Contains(x.EmployeeId))
                .Select(x => new
                {
                    x.EmployeeId,
                    x.FullName
                })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, cancellationToken);

        var canViewProductTechnicalInfo = _fieldVisibility.CanViewProductTechnicalInfo();

        var timeline = auditLogs
            .GroupBy(x => x.CorrelationId ?? x.AuditLogId)
            .Select(x => ToDto(
                request.SampleRequestId,
                x,
                employeeNamesById,
                canViewProductTechnicalInfo))
            .Where(x => x.Details.Count > 0)
            .OrderByDescending(x => x.ChangedAt)
            .ThenByDescending(x => x.AuditLogId)
            .ToList();

        await ResolveFormulaDisplayValuesAsync(timeline, sampleRequest.CompanyId, cancellationToken);

        return new SampleRequestHistoryResponseDto
        {
            LatestChange = timeline.FirstOrDefault(),
            Fields = BuildFieldHistory(timeline),
            Timeline = timeline
        };
    }

    private async Task ResolveFormulaDisplayValuesAsync(
        IReadOnlyList<SampleRequestHistoryDto> timeline,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        var formulaIds = timeline
            .SelectMany(x => x.Details)
            .Where(x => string.Equals(x.FieldName, "FormulaId", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => new[] { x.OldValue, x.NewValue })
            .Where(x => Guid.TryParse(x, out _))
            .Select(x => Guid.Parse(x!))
            .Distinct()
            .ToList();

        if (formulaIds.Count == 0)
        {
            return;
        }

        var formulaDisplayById = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x => formulaIds.Contains(x.FormulaId) && x.CompanyId == companyId)
            .Select(x => new { x.FormulaId, x.ExternalId, x.Name })
            .ToDictionaryAsync(
                x => x.FormulaId,
                x => string.IsNullOrWhiteSpace(x.Name) ? x.ExternalId : $"{x.ExternalId} - {x.Name}",
                cancellationToken);

        foreach (var entry in timeline)
        {
            entry.Details = entry.Details
                .Select(detail => ResolveFormulaDisplayValue(detail, formulaDisplayById))
                .ToList();
        }
    }

    private static SampleRequestHistoryDetailDto ResolveFormulaDisplayValue(
        SampleRequestHistoryDetailDto detail,
        IReadOnlyDictionary<Guid, string> formulaDisplayById)
    {
        if (!string.Equals(detail.FieldName, "FormulaId", StringComparison.OrdinalIgnoreCase))
        {
            return detail;
        }

        return new SampleRequestHistoryDetailDto
        {
            Source = detail.Source,
            FieldName = detail.FieldName,
            OldValue = ResolveFormulaDisplayValue(detail.OldValue, formulaDisplayById),
            NewValue = ResolveFormulaDisplayValue(detail.NewValue, formulaDisplayById)
        };
    }

    private static string? ResolveFormulaDisplayValue(
        string? value,
        IReadOnlyDictionary<Guid, string> formulaDisplayById)
    {
        if (value is null || !Guid.TryParse(value, out var formulaId))
        {
            return value;
        }

        return formulaDisplayById.TryGetValue(formulaId, out var displayValue)
            ? displayValue
            : "Công thức không còn tồn tại";
    }

    private static IReadOnlyList<SampleRequestHistoryFieldDto> BuildFieldHistory(
        IReadOnlyList<SampleRequestHistoryDto> timeline)
    {
        return timeline
            .SelectMany(entry => entry.Details.Select(detail => new { Entry = entry, Detail = detail }))
            .GroupBy(x => new { x.Detail.Source, x.Detail.FieldName })
            .Select(group =>
            {
                var changes = group
                    .OrderBy(x => x.Entry.ChangedAt)
                    .ThenBy(x => x.Entry.AuditLogId)
                    .ToList();
                var latest = changes[^1];

                return new SampleRequestHistoryFieldDto
                {
                    Source = group.Key.Source,
                    FieldName = group.Key.FieldName,
                    InitialValue = changes[0].Detail.OldValue,
                    CurrentValue = latest.Detail.NewValue,
                    ChangeCount = changes.Count,
                    LastChangedAt = latest.Entry.ChangedAt,
                    LastChangedByName = latest.Entry.ChangedByName
                };
            })
            .OrderByDescending(x => x.LastChangedAt)
            .ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SampleRequestHistoryDto ToDto(
        Guid sampleRequestId,
        IGrouping<Guid, AuditLog> auditLogGroup,
        IReadOnlyDictionary<Guid, string> employeeNamesById,
        bool canViewProductTechnicalInfo)
    {
        var auditLogs = auditLogGroup
            .OrderByDescending(x => x.TableName == SampleRequestsTable)
            .ThenByDescending(x => x.ChangedAt)
            .ThenByDescending(x => x.AuditLogId)
            .ToList();
        var firstLog = auditLogs[0];
        var actionTypes = auditLogs
            .Select(x => x.ActionType.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var details = auditLogs
            .SelectMany(BuildDetails)
            .Where(detail => CanViewDetail(detail, canViewProductTechnicalInfo))
            .ToList();
        var sources = details
            .Select(x => x.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => string.Equals(x, SampleRequestsTable, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SampleRequestHistoryDto
        {
            AuditLogId = firstLog.AuditLogId,
            Source = sources.Count == 1 ? sources[0] : SampleRequestsTable,
            Sources = sources,
            RecordId = sources.Count == 1 ? firstLog.RecordId : sampleRequestId,
            ActionType = actionTypes.Count == 1 ? actionTypes[0] : "Mixed",
            ChangedBy = firstLog.ChangedBy,
            ChangedByName = firstLog.ChangedBy.HasValue
                && employeeNamesById.TryGetValue(firstLog.ChangedBy.Value, out var changedByName)
                    ? changedByName
                    : null,
            ChangedAt = auditLogs.Max(x => x.ChangedAt),
            Reason = auditLogs
                .Select(x => x.Reason)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            CorrelationId = firstLog.CorrelationId,
            Details = details
        };
    }

    private static bool CanViewDetail(
        SampleRequestHistoryDetailDto detail,
        bool canViewProductTechnicalInfo)
    {
        if (canViewProductTechnicalInfo)
        {
            return true;
        }

        return !string.Equals(detail.Source, ProductsTable, StringComparison.OrdinalIgnoreCase) ||
            !RestrictedProductHistoryFields.Contains(detail.FieldName);
    }

    private static IReadOnlyList<SampleRequestHistoryDetailDto> BuildDetails(AuditLog auditLog)
    {
        var changedValues = auditLog.ChangedValues?.RootElement;
        var oldValues = auditLog.OldValues?.RootElement;
        var newValues = auditLog.NewValues?.RootElement;

        if (changedValues is { ValueKind: JsonValueKind.Object })
        {
            return changedValues.Value.EnumerateObject()
                .Select(property => BuildDetail(auditLog.TableName, property, oldValues, newValues))
                .Where(detail => detail is not null)
                .Select(detail => detail!)
                .ToList();
        }

        if (oldValues is { ValueKind: JsonValueKind.Object } && newValues is { ValueKind: JsonValueKind.Object })
        {
            return oldValues.Value.EnumerateObject()
                .Select(property => BuildDetailFromSnapshots(auditLog.TableName, property, newValues.Value))
                .Where(detail => detail is not null)
                .Select(detail => detail!)
                .ToList();
        }

        return [];
    }

    private static SampleRequestHistoryDetailDto? BuildDetail(
        string source,
        JsonProperty changedProperty,
        JsonElement? oldValues,
        JsonElement? newValues)
    {
        var fieldName = changedProperty.Name;
        var changedValue = changedProperty.Value;

        if (changedValue.ValueKind == JsonValueKind.Object)
        {
            var hasOld = TryGetPropertyByNames(changedValue, out var oldValue, "oldValue", "OldValue", "old", "Old");
            var hasNew = TryGetPropertyByNames(changedValue, out var newValue, "newValue", "NewValue", "new", "New");

            if (hasOld || hasNew)
            {
                return CreateDetail(source, fieldName, hasOld ? oldValue : null, hasNew ? newValue : null);
            }
        }

        var hasOldSnapshot = TryGetSnapshotValue(oldValues, fieldName, out var oldSnapshotValue);
        var hasNewSnapshot = TryGetSnapshotValue(newValues, fieldName, out var newSnapshotValue);

        return CreateDetail(
            source,
            fieldName,
            hasOldSnapshot ? oldSnapshotValue : null,
            hasNewSnapshot ? newSnapshotValue : changedValue);
    }

    private static SampleRequestHistoryDetailDto? BuildDetailFromSnapshots(
        string source,
        JsonProperty oldProperty,
        JsonElement newValues)
    {
        if (!TryGetPropertyCaseInsensitive(newValues, oldProperty.Name, out var newValue))
        {
            return null;
        }

        if (JsonElementEquals(oldProperty.Value, newValue))
        {
            return null;
        }

        return CreateDetail(source, oldProperty.Name, oldProperty.Value, newValue);
    }

    private static bool TryGetSnapshotValue(JsonElement? snapshot, string fieldName, out JsonElement value)
    {
        value = default;

        return snapshot is { ValueKind: JsonValueKind.Object }
            && TryGetPropertyCaseInsensitive(snapshot.Value, fieldName, out value);
    }

    private static bool TryGetPropertyByNames(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetPropertyCaseInsensitive(element, name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static SampleRequestHistoryDetailDto? CreateDetail(
        string source,
        string fieldName,
        JsonElement? oldValue,
        JsonElement? newValue)
    {
        if (oldValue.HasValue && newValue.HasValue && JsonElementEquals(oldValue.Value, newValue.Value))
        {
            return null;
        }

        return new SampleRequestHistoryDetailDto
        {
            Source = source,
            FieldName = fieldName,
            OldValue = FormatValue(oldValue),
            NewValue = FormatValue(newValue)
        };
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        return string.Equals(FormatValue(left), FormatValue(right), StringComparison.Ordinal);
    }

    private static string? FormatValue(JsonElement? value)
    {
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => value.Value.GetRawText(),
            _ => value.Value.GetRawText()
        };
    }
}
