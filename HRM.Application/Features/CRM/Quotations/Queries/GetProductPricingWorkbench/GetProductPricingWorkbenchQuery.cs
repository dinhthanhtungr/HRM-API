using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbench;

public sealed class GetProductPricingWorkbenchQuery
    : PaginationQuery,
        IRequest<OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>>
{
    public const string StandardPricingCurrency = ProductPricingSourceRules.StandardPricingCurrency;

    public string? Currency { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingWorkbenchSearchType? SearchType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestStatus[] SampleStatuses { get; init; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestStatus? Status { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingWorkbenchView View { get; init; } =
        ProductPricingWorkbenchView.All;

    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Color { get; init; }
    public string? AdditiveCode { get; init; }

    [JsonIgnore]
    public string NormalizedCurrency => string.IsNullOrWhiteSpace(Currency)
        ? StandardPricingCurrency
        : Currency.Trim().ToUpperInvariant();

    [JsonIgnore]
    public ProductPricingWorkbenchSearchType EffectiveSearchType => SearchType ??
        InferSearchType(NormalizedKeyword);

    internal static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductPricingWorkbenchSearchType InferSearchType(string? keyword)
    {
        if (keyword is null)
        {
            return ProductPricingWorkbenchSearchType.All;
        }

        return keyword.ToUpperInvariant() switch
        {
            var value when value.StartsWith("BBG", StringComparison.Ordinal) =>
                ProductPricingWorkbenchSearchType.Quotation,
            var value when value.StartsWith("KH_", StringComparison.Ordinal) =>
                ProductPricingWorkbenchSearchType.Customer,
            var value when value.StartsWith("TP_", StringComparison.Ordinal) =>
                ProductPricingWorkbenchSearchType.SampleRequest,
            var value when value.StartsWith("TL", StringComparison.Ordinal) =>
                ProductPricingWorkbenchSearchType.Product,
            var value when value.StartsWith("VU", StringComparison.Ordinal) =>
                ProductPricingWorkbenchSearchType.Formula,
            _ => ProductPricingWorkbenchSearchType.All
        };
    }
}
