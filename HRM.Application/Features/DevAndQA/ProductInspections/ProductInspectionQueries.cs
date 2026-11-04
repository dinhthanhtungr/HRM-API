using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using System.Linq.Expressions;

namespace HRM.Application.Features.DevAndQA.ProductInspections;

/// <summary>Scope phiếu theo company đã lưu; chỉ fallback tiêu chuẩn cho phiếu legacy.</summary>
internal static class ProductInspectionQueries
{
    internal static IQueryable<ProductInspection> ForCompany(this IQueryable<ProductInspection> query,
        IPLMDbContext db, Guid companyId)
        => query.Where(x => x.CompanyId == companyId ||
            (x.CompanyId == null && x.ProductStandardId.HasValue &&
                db.ProductStandards.Any(s => s.Id == x.ProductStandardId && s.CompanyId == companyId)));

    internal static readonly Expression<Func<ProductStandard, ProductInspectionPdfSpecificationsDto>> Specifications = x =>
        new ProductInspectionPdfSpecificationsDto
        {
            Shape = x.Shape, PelletSize = x.PelletSize, DeltaE = x.DeltaE,
            Moisture = x.Moisture, Density = x.Density, MeltIndex = x.MeltIndex,
            TensileStrength = x.TensileStrength, ElongationAtBreak = x.ElongationAtBreak,
            FlexuralStrength = x.FlexuralStrength, FlexuralModulus = x.FlexuralModulus,
            IzodImpactStrength = x.IzodImpactStrength, Hardness = x.Hardness,
            DwellTime = x.DwellTime, BlackDots = x.BlackDots, MigrationTest = x.MigrationTest
        };
}
