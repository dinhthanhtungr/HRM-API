using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialPriceReviews;

internal static class MaterialPriceReviewSmartSearch
{
    internal static SmartSearchSchema<Material> Build(IQueryable<Material> visible, Guid companyId, bool canViewDetails)
        => SmartSearch.For(visible, fields => {
            var result = new List<SmartSearchField<Material>> {
                fields.Code("materialCode", x => x.ExternalId, "Mã NVL", "Material code"),
                fields.Name("materialName", x => x.Name, "Tên NVL", "Material name")
            };
            if (canViewDetails)
            {
                result.Add(fields.Codes("supplierCode", x => x.MaterialsSuppliers.Where(s => s.IsActive == true &&
                    s.Supplier.CompanyId == companyId && s.Supplier.IsActive == true), s => s.Supplier.ExternalId, "Mã NCC", "Supplier code"));
                result.Add(fields.Names("supplierName", x => x.MaterialsSuppliers.Where(s => s.IsActive == true &&
                    s.Supplier.CompanyId == companyId && s.Supplier.IsActive == true), s => s.Supplier.SupplierName, "Tên NCC", "Supplier name"));
            }
            return [..result];
        });
}
