using System.Text.Json.Nodes;
using HRM.Api.Controllers.Purchasing;
using HRM.Domain.Enums.Category;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HRM.Api.OpenApi;

public sealed class PurchasingCategoryOptionsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(PurchasingCatalogOptionsController)
            || context.MethodInfo.Name != nameof(PurchasingCatalogOptionsController.GetCategories)) return;

        foreach (var parameter in (operation.Parameters ?? []).OfType<OpenApiParameter>())
        {
            if (!string.Equals(parameter.Name, "Type", StringComparison.OrdinalIgnoreCase)) continue;
            parameter.Name = "type";
            parameter.Required = false;
            parameter.Description = "Product = 0, NVL = 1, VTBT = 2. Omit to return all active categories in the current company. Invalid values return 400.";
            parameter.Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = Enum.GetNames<CategoryTypes>().Select(value => (JsonNode)JsonValue.Create(value)!).ToList()
            };
            parameter.Example = JsonValue.Create(nameof(CategoryTypes.NVL));
        }
        operation.Responses ??= new();
        operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Invalid category type." });
    }
}
