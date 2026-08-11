using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Audits;

namespace HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;

internal static class SampleRequestDataChangeAuditHelper
{
    private const string SampleRequestAuditSchema = "SampleRequests";
    private const string SampleRequestAuditSource = "SampleRequests";
    private const string ProductAuditSource = "Products";

    public static Dictionary<string, object?> BuildSampleRequestAuditSnapshot(SampleRequest entity)
    {
        return new Dictionary<string, object?>
        {
            ["CustomerId"] = entity.CustomerId,
            ["ProductId"] = entity.ProductId,
            ["FormulaId"] = entity.FormulaId,
            ["Status"] = entity.Status,
            ["RequestType"] = entity.RequestType,
            ["ExpectedQuantity"] = entity.ExpectedQuantity,
            ["ExpectedPrice"] = entity.ExpectedPrice,
            ["SampleQuantity"] = entity.SampleQuantity,
            ["Package"] = entity.Package,
            ["BagWeight"] = entity.BagWeight,
            ["InfoType"] = entity.InfoType,
            ["CustomerProductCode"] = entity.CustomerProductCode,
            ["AdditionalComment"] = entity.AdditionalComment,
            ["SaleComment"] = entity.SaleComment,
            ["OtherComment"] = entity.OtherComment,
            ["RealDeliveryDate"] = entity.RealDeliveryDate,
            ["RequestTestSampleDate"] = entity.RequestTestSampleDate,
            ["ExpectedDeliveryDate"] = entity.ExpectedDeliveryDate,
            ["RequestDeliveryDate"] = entity.RequestDeliveryDate,
            ["ResponseDeliveryDate"] = entity.ResponseDeliveryDate,
            ["RealPriceQuoteDate"] = entity.RealPriceQuoteDate,
            ["ExpectedPriceQuoteDate"] = entity.ExpectedPriceQuoteDate,
            ["NumberDeliverySampleDate"] = entity.NumberDeliverySampleDate,
            ["BranchId"] = entity.BranchId
        };
    }

    public static Dictionary<string, object?> BuildProductAuditSnapshot(Product product)
    {
        return new Dictionary<string, object?>
        {
            ["Name"] = product.Name,
            ["ColourCode"] = product.ColourCode,
            ["ColourName"] = product.ColourName,
            ["Requirement"] = product.Requirement,
            ["ExpiryType"] = product.ExpiryType,
            ["LabComment"] = product.LabComment,
            ["Procedure"] = product.Procedure,
            ["Application"] = product.Application,
            ["ProductUsage"] = product.ProductUsage,
            ["PolymerMatchedIn"] = product.PolymerMatchedIn,
            ["Code"] = product.Code,
            ["EndUser"] = product.EndUser,
            ["OtherComment"] = product.OtherComment,
            ["Unit"] = product.Unit,
            ["StorageCondition"] = product.StorageCondition,
            ["UsageRate"] = product.UsageRate,
            ["DeltaE"] = product.DeltaE,
            ["RecycleRate"] = product.RecycleRate,
            ["TaicalRate"] = product.TaicalRate,
            ["MaxTemp"] = product.MaxTemp,
            ["Weight"] = product.Weight,
            ["FoodSafety"] = product.FoodSafety,
            ["RohsStandard"] = product.RohsStandard,
            ["WeatherResistance"] = product.WeatherResistance,
            ["LightCondition"] = product.LightCondition,
            ["VisualTest"] = product.VisualTest,
            ["ReturnSample"] = product.ReturnSample,
            ["CategoryId"] = product.CategoryId,
            ["IsRecycle"] = product.IsRecycle,
            ["ReachStandard"] = product.ReachStandard,
            ["Additive"] = product.Additive
        };
    }

    public static Task AddSampleRequestAuditIfChangedAsync(
        IPLMWriteDbContext dbContext,
        SampleRequest sampleRequest,
        Guid? changedBy,
        DateTime changedAt,
        Guid correlationId,
        Dictionary<string, object?> oldValues,
        string reason,
        CancellationToken cancellationToken)
    {
        return AddAuditIfChangedAsync(
            dbContext,
            SampleRequestAuditSource,
            sampleRequest.SampleRequestId,
            sampleRequest.CompanyId,
            changedBy,
            changedAt,
            correlationId,
            oldValues,
            BuildSampleRequestAuditSnapshot(sampleRequest),
            reason,
            cancellationToken);
    }

    public static Task AddProductAuditIfChangedAsync(
        IPLMWriteDbContext dbContext,
        Product product,
        Guid? changedBy,
        DateTime changedAt,
        Guid correlationId,
        Dictionary<string, object?> oldValues,
        string reason,
        CancellationToken cancellationToken)
    {
        return AddAuditIfChangedAsync(
            dbContext,
            ProductAuditSource,
            product.ProductId,
            product.CompanyId,
            changedBy,
            changedAt,
            correlationId,
            oldValues,
            BuildProductAuditSnapshot(product),
            reason,
            cancellationToken);
    }

    private static async Task AddAuditIfChangedAsync(
        IPLMWriteDbContext dbContext,
        string tableName,
        Guid recordId,
        Guid? companyId,
        Guid? changedBy,
        DateTime changedAt,
        Guid correlationId,
        Dictionary<string, object?> oldValues,
        Dictionary<string, object?> newValues,
        string reason,
        CancellationToken cancellationToken)
    {
        var changedValues = BuildChangedValues(oldValues, newValues);
        if (changedValues.Count == 0)
        {
            return;
        }

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.CreateVersion7(),
            CompanyId = companyId,
            SchemaName = SampleRequestAuditSchema,
            TableName = tableName,
            RecordId = recordId,
            ActionType = AuditActionType.Update,
            ChangedBy = changedBy,
            ChangedAt = changedAt,
            OldValues = JsonSerializer.SerializeToDocument(oldValues),
            NewValues = JsonSerializer.SerializeToDocument(newValues),
            ChangedValues = JsonSerializer.SerializeToDocument(changedValues),
            Reason = reason,
            CorrelationId = correlationId
        }, cancellationToken);
    }

    private static Dictionary<string, object?> BuildChangedValues(
        Dictionary<string, object?> oldValues,
        Dictionary<string, object?> newValues)
    {
        var result = new Dictionary<string, object?>();

        foreach (var key in oldValues.Keys.Union(newValues.Keys))
        {
            oldValues.TryGetValue(key, out var oldValue);
            newValues.TryGetValue(key, out var newValue);

            if (!AuditValueEquals(oldValue, newValue))
            {
                result[key] = new
                {
                    Old = oldValue,
                    New = newValue
                };
            }
        }

        return result;
    }

    private static bool AuditValueEquals(object? oldValue, object? newValue)
    {
        if (oldValue is null && newValue is null)
        {
            return true;
        }

        if (oldValue is null || newValue is null)
        {
            return false;
        }

        return JsonSerializer.Serialize(oldValue) == JsonSerializer.Serialize(newValue);
    }
}
