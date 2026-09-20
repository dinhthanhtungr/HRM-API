namespace HRM.Application.Features.CRM.Quotations.Dtos;

/// <summary>
/// Giới hạn keyword vào đúng nguồn định danh nghiệp vụ của pricing workbench.
/// </summary>
public enum ProductPricingWorkbenchSearchType
{
    All = 0,
    Quotation = 1,
    Customer = 2,
    SampleRequest = 3,
    Product = 4,
    Formula = 5
}
