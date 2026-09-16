namespace HRM.Application.Features.Executive.SampleRequestPricingOverview.Dtos;

/// <summary>
/// Narrows the keyword query to its business identifier source. All preserves free-text search.
/// </summary>
public enum SampleRequestPricingOverviewSearchType
{
    All = 0,
    Quotation = 1,
    Customer = 2,
    SampleRequest = 3,
    Product = 4,
    Formula = 5
}
