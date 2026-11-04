using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulas;

internal sealed class ManufacturingFormulaHeader
{
    public Guid Id { get; set; }
    public FormulaSource Source { get; set; }
    public Guid ProductId { get; set; }
    public Guid? OrderId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ColourCode { get; set; }
    public DateTime CreatedDate { get; set; }
}

internal static class ManufacturingFormulaHeaderQuery
{
    public static IQueryable<ManufacturingFormulaHeader> Build(
        IManufacturingDbContext db,
        Guid company,
        ManufacturingFormulaListOptions r,
        bool compare
    )
    {
        var pattern = string.IsNullOrWhiteSpace(r.Keyword) ? null : $"%{r.Keyword.Trim()}%";
        var vu = db.Formulas.AsNoTracking().Where(x => x.CompanyId == company && x.Product.CompanyId == company && x.IsActive
            && (!r.ProductId.HasValue || x.ProductId == r.ProductId)
            && (!r.From.HasValue || x.CreatedDate >= r.From)
            && (!r.To.HasValue || x.CreatedDate <= r.To)
            && (pattern == null || EF.Functions.ILike(x.ExternalId, pattern) || EF.Functions.ILike(x.Name, pattern)
            || EF.Functions.ILike(x.Product.ColourCode ?? "", pattern)
            || EF.Functions.ILike(x.Product.Name ?? "", pattern)
            || x.Product.SampleRequests.Any(s => s.IsActive && s.CompanyId == company && EF.Functions.ILike(s.ExternalId, pattern))))
            .Select(x => new ManufacturingFormulaHeader
        {
            Id = x.FormulaId,
            Source = FormulaSource.FromVU,
            ProductId = x.ProductId,
            OrderId = null,
            ExternalId = x.ExternalId,
            Name = x.Name,
            ColourCode = x.Product.ColourCode,
            CreatedDate = x.CreatedDate ?? DateTime.MinValue
        });
        IQueryable<ManufacturingFormulaHeader> va;
        if (compare)
        {
            va =
                from s in db.ProductStandardFormulas.AsNoTracking()join f in db.ManufacturingFormulas.AsNoTracking()on s.ManufacturingFormulaId equals f.ManufacturingFormulaId
                join p in db.Products.AsNoTracking()on s.ProductId equals p.ProductId
                where s.CompanyId == company && f.CompanyId == company && p.CompanyId == company && f.IsActive
                && (!r.ProductId.HasValue || p.ProductId == r.ProductId)
                && (!r.From.HasValue || f.CreatedDate >= r.From)
                && (!r.To.HasValue || f.CreatedDate <= r.To)
                && !db.ProductStandardFormulas.Any(other => other.CompanyId == company && other.ManufacturingFormulaId == s.ManufacturingFormulaId
                && (other.ValidFrom > s.ValidFrom
                || other.ValidFrom == s.ValidFrom
                && other.ProductStandardFormulaId.CompareTo(s.ProductStandardFormulaId) > 0))
                && (pattern == null || EF.Functions.ILike(f.ExternalId, pattern) || EF.Functions.ILike(f.Name, pattern)
                || EF.Functions.ILike(p.ColourCode ?? "", pattern)
                || EF.Functions.ILike(p.Name ?? "", pattern)
                || p.SampleRequests.Any(sr => sr.IsActive && sr.CompanyId == company && EF.Functions.ILike(sr.ExternalId, pattern)))select new ManufacturingFormulaHeader

            {

                Id = f.ManufacturingFormulaId,

                Source = FormulaSource.FromVA,

                ProductId = p.ProductId,

                OrderId = null,

                ExternalId = f.ExternalId,

                Name = f.Name,

                ColourCode = p.ColourCode,

                CreatedDate = f.CreatedDate

            };
        }
        else
        {
            va =
                from v in db.ProductionSelectVersions.AsNoTracking()join o in db.MfgProductionOrders.AsNoTracking()on v.MfgProductionOrderId equals o.MfgProductionOrderId
                join f in db.ManufacturingFormulas.AsNoTracking()on v.ManufacturingFormulaId equals f.ManufacturingFormulaId
                where v.CompanyId == company && o.CompanyId == company && f.CompanyId == company && o.IsActive
                && f.IsActive
                && o.Product.CompanyId == company
                && (!r.ProductId.HasValue || o.ProductId == r.ProductId)
                && (!r.From.HasValue || f.CreatedDate >= r.From)
                && (!r.To.HasValue || f.CreatedDate <= r.To)
                && (pattern == null || EF.Functions.ILike(f.ExternalId, pattern)
                || EF.Functions.ILike(o.FormulaExternalIdSnapshot ?? "", pattern)
                || EF.Functions.ILike(o.Product.ColourCode ?? "", pattern)
                || EF.Functions.ILike(o.ColorName ?? o.Product.ColourName ?? "", pattern)
                || EF.Functions.ILike(o.CustomerNameSnapshot ?? "", pattern)
                || o.Product.SampleRequests.Any(sr => sr.IsActive && sr.CompanyId == company && EF.Functions.ILike(sr.ExternalId, pattern)))select new ManufacturingFormulaHeader

            {

                Id = f.ManufacturingFormulaId,

                Source = FormulaSource.FromVA,

                ProductId = o.ProductId,

                OrderId = o.MfgProductionOrderId,

                ExternalId = f.ExternalId,

                Name = f.Name,

                ColourCode = o.Product.ColourCode,

                CreatedDate = f.CreatedDate

            };
        } // List retains one VA row per PSV history as legacy; Compare uses latest PSF per VA,

        // including a closed PSF. Neither path assumes a single open standard per VA.
        return r.Source switch
        {
            FormulaSource.FromVA => va,
            FormulaSource.FromVU => vu,
            _ => va.Concat(vu)};
    }
}
