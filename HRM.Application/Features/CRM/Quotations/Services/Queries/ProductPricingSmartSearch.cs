using HRM.Application.Commons.Searching;
using HRM.Application.Features.CRM.CustomerCare.Services.Search;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services.Queries;

internal static class ProductPricingSmartSearch
{
    internal static SmartSearchSchema<Product> Build(IQueryable<Product> products,
        IQueryable<SampleRequest> visibleRequests, Guid companyId,
        IQueryable<ProductPricingVersion>? visibleVersions = null) => SmartSearch.For(products, fields => [
        fields.Code("colourCode", x => x.ColourCode, "ColorCode", "Mã màu"),
        fields.Name("productName", x => x.Name, "Tên sản phẩm", "Product name"),
        visibleVersions is null
            ? fields.Codes("formulaCode", x => x.Formulas.Where(f => f.CompanyId == companyId && f.IsActive), f => f.ExternalId, "Công thức", "Formula", "VU")
            : fields.Codes("formulaCode", x => visibleVersions.Where(v => v.ProductId == x.ProductId), v => v.FormulaExternalIdSnapshot, "Công thức", "Formula", "VU"),
        fields.Codes("sampleRequestCode", x => visibleRequests.Where(s => s.ProductId == x.ProductId), s => s.ExternalId, "TP Code", "Mã TP"),
        fields.Codes("customerCode", x => visibleRequests.Where(s => s.ProductId == x.ProductId && s.Customer.CompanyId == companyId),
            s => s.Customer.ExternalId, "Mã KH", "Customer code"),
        fields.Names("customerName", x => visibleRequests.Where(s => s.ProductId == x.ProductId && s.Customer.CompanyId == companyId),
            s => s.Customer.CustomerName, "Tên KH", "Customer name"),
        fields.CustomerTaxNumbers(x => visibleRequests.Where(s => s.ProductId == x.ProductId && s.Customer.CompanyId == companyId),
            s => s.Customer.TaxNumber)
    ]);
}
