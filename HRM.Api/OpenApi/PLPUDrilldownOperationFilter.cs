using System.Text.Json.Nodes;
using HRM.Api.Controllers.Reports;
using HRM.Application.Features.Reports.PLPU;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HRM.Api.OpenApi;

public sealed class PLPUDrilldownOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(PLPUReportController)
            || context.MethodInfo.Name != nameof(PLPUReportController.GetDrilldown)) return;

        operation.Description = "ReportStatus filters the computed primary PO status grouped by /body.statusDistribution. "
            + "Optional, case-sensitive, only supported with Metric=totalPo. Status remains the persisted PO status. "
            + "Filtering happens before totals and pagination. Example: "
            + "/api/v1/reports/plpu/drilldown?Metric=totalPo&ReportStatus=Completed&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20. "
            + string.Join("; ", PLPUReportStatuses.Labels.Select(x => x.Key + " = " + x.Value));
        foreach (var parameter in (operation.Parameters ?? []).OfType<OpenApiParameter>())
        {
            if (!string.Equals(parameter.Name, "ReportStatus", StringComparison.OrdinalIgnoreCase)) continue;
            parameter.Description = "Computed status code (not Vietnamese label); only Metric=totalPo. Omit to preserve existing behavior.";
            parameter.Required = false;
            parameter.Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = PLPUReportStatuses.Labels.Keys.Select(code => (JsonNode)JsonValue.Create(code)!).ToList()
            };
            parameter.Example = JsonValue.Create("Completed");
        }
        if (operation.Responses?.TryGetValue("200", out var response) == true
            && response.Content?.TryGetValue("application/json", out var media) == true)
            media.Example = JsonNode.Parse("""
                {"companyId":"20000000-0000-0000-0000-000000000001","metric":"totalPo","rowType":"order",
                "metricValue":1,"pageValue":1,"totalCount":1,"page":1,"pageSize":20,"totalPages":1,
                "items":[{"contribution":1,"reportStatusCode":"Completed","lineStatusCode":null,
                "order":{"purchaseOrderId":"30000000-0000-0000-0000-000000000001","poExternalId":"DDH-1","reportStatus":"Hoàn thành"},"line":null}]}
                """);
    }
}
