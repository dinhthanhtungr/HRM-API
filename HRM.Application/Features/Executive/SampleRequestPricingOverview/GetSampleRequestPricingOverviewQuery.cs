using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.SampleRequestPricingOverview.Dtos;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.Executive.SampleRequestPricingOverview;

public sealed class GetSampleRequestPricingOverviewQuery
    : IRequest<OperationResult<PagedResult<SampleRequestPricingOverviewItemDto>>>
{
    private const int DefaultPageSize = 12;
    private const int MaximumPageSize = 100;

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public string? Keyword { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestPricingOverviewSearchType? SearchType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestStatus[] SampleStatuses { get; init; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestStatus? Status { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingWorkbenchView View { get; init; } = ProductPricingWorkbenchView.All;

    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Color { get; init; }
    public string? AdditiveCode { get; init; }
    public string? Currency { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }

    [JsonIgnore]
    public int NormalizedPageNumber => Math.Max(1, PageNumber);

    [JsonIgnore]
    public int NormalizedPageSize => PageSize < 1
        ? DefaultPageSize
        : Math.Min(PageSize, MaximumPageSize);

    [JsonIgnore]
    public string? NormalizedKeyword => Normalize(Keyword);

    [JsonIgnore]
    public SampleRequestPricingOverviewSearchType EffectiveSearchType => SearchType ??
        InferSearchType(NormalizedKeyword);

    [JsonIgnore]
    public string NormalizedCurrency => Normalize(Currency)?.ToUpperInvariant() ?? "VND";

    [JsonIgnore]
    public string NormalizedSortBy => Normalize(SortBy) ?? "createdDate";

    [JsonIgnore]
    public bool SortDescending => !string.Equals(
        SortDirection,
        "asc",
        StringComparison.OrdinalIgnoreCase);

    internal static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SampleRequestPricingOverviewSearchType InferSearchType(string? keyword)
    {
        if (keyword is null)
        {
            return SampleRequestPricingOverviewSearchType.All;
        }

        return keyword.ToUpperInvariant() switch
        {
            var value when value.StartsWith("BBG", StringComparison.Ordinal) =>
                SampleRequestPricingOverviewSearchType.Quotation,
            var value when value.StartsWith("KH_", StringComparison.Ordinal) =>
                SampleRequestPricingOverviewSearchType.Customer,
            var value when value.StartsWith("TP_", StringComparison.Ordinal) =>
                SampleRequestPricingOverviewSearchType.SampleRequest,
            var value when value.StartsWith("TL", StringComparison.Ordinal) =>
                SampleRequestPricingOverviewSearchType.Product,
            var value when value.StartsWith("VU", StringComparison.Ordinal) =>
                SampleRequestPricingOverviewSearchType.Formula,
            _ => SampleRequestPricingOverviewSearchType.All
        };
    }
}
