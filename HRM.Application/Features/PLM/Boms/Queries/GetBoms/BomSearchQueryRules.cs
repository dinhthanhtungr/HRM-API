using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBoms;

internal static class BomSearchQueryRules
{
    internal static IQueryable<BomDefinition> Build(IPLMReadDbContext db, Guid companyId, Guid? productId, BomType type,
        string? keyword, ISmartSearchRequest request, out SmartSearchSchema<BomDefinition> schema)
    {
        var rows = db.BomDefinitions.AsNoTracking().Where(x => x.CompanyId == companyId && x.BomType == type && x.IsActive &&
            (!productId.HasValue || x.ProductId == productId));
        schema = SmartSearch.For(rows, fields => [
            fields.Code("bomCode", x => x.ExternalId, "Mã BOM", "BOM code"),
            fields.Name("bomName", x => x.Name, "Tên BOM", "BOM name"),
            fields.Name("productName", x => x.Product.CompanyId == companyId ? x.Product.Name : "", "Tên sản phẩm", "Product name"),
            fields.Code("colourCode", x => x.Product.CompanyId == companyId ? x.Product.ColourCode : "", "ColorCode", "Mã màu"),
            fields.Codes("formulaCode", x => x.Versions, v => v.SourceFormula != null && v.SourceFormula.CompanyId == companyId && v.SourceFormula.IsActive
                ? v.SourceFormula.ExternalId : v.SourceEngineeringBomVersion != null && v.SourceEngineeringBomVersion.BomDefinition.CompanyId == companyId &&
                    v.SourceEngineeringBomVersion.SourceFormula != null && v.SourceEngineeringBomVersion.SourceFormula.CompanyId == companyId &&
                    v.SourceEngineeringBomVersion.SourceFormula.IsActive ? v.SourceEngineeringBomVersion.SourceFormula.ExternalId : "",
                "Công thức", "Formula", "VU")
        ]);
        return schema.ApplyRequest(rows, keyword, request, out var legacy)
            .WhereKeyword(legacy, term => x => EF.Functions.ILike(x.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
    }
}
