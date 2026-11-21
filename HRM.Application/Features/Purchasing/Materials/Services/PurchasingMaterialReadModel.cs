using System.Linq.Expressions;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Rules;
using HRM.Application.Features.Purchasing.Materials.Dtos;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Materials;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Materials.Services;

internal static class PurchasingMaterialReadModel
{
    internal static IQueryable<Material> Filter(IQueryable<Material> materials, Guid companyId, GetPurchasingMaterialsQuery request, bool includeKeyword = true)
    {
        if (request.PurchaseStatus.HasValue && !Enum.IsDefined(request.PurchaseStatus.Value))
            throw new ArgumentException("Trạng thái mua không hợp lệ.", nameof(request));
        var query = materials.AsNoTracking().Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive == true));
        if (request.CategoryId.HasValue) query = query.Where(x => x.CategoryId == request.CategoryId.Value);
        if (request.SupplierId.HasValue)
            query = query.Where(x => x.MaterialsSuppliers.Any(s => s.IsActive == true && s.SupplierId == request.SupplierId.Value &&
                s.Supplier.CompanyId == companyId && s.Supplier.IsActive == true));
        if (request.PurchaseStatus.HasValue)
            query = query.Where(x => (x.PurchaseAvailability == null ? MaterialPurchaseStatus.Available : x.PurchaseAvailability.Status) == request.PurchaseStatus.Value);
        return query.WhereKeyword(includeKeyword ? request.Keyword : null, term => x =>
            EF.Functions.ILike(x.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.CustomCode ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Barcode ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
    }

    internal static IOrderedQueryable<Material> Order(IQueryable<Material> query, PaginationQuery request)
    {
        var key = request.NormalizedSortBy?.ToLowerInvariant();
        var ordered = key switch
        {
            "name" => request.SortDescending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "createddate" => request.SortDescending ? query.OrderByDescending(x => x.CreatedDate) : query.OrderBy(x => x.CreatedDate),
            _ => request.SortDescending ? query.OrderByDescending(x => x.ExternalId) : query.OrderBy(x => x.ExternalId)
        };
        return ordered.ThenBy(x => x.MaterialId);
    }

    internal static Expression<Func<Material, PurchasingMaterialSummaryDto>> Summary(Guid companyId) => x => new()
    {
        IsActive = x.IsActive == true,
        MaterialId = x.MaterialId, ExternalId = x.ExternalId, CustomCode = x.CustomCode, Name = x.Name,
        CategoryId = x.CategoryId, CategoryName = x.Category.CompanyId == companyId ? x.Category.Name : null,
        Unit = x.Unit, Package = x.Package, Weight = x.Weight, MinQuantity = x.MinQuantity,
        SupplierCount = x.MaterialsSuppliers.Count(s => s.IsActive == true && s.Supplier.CompanyId == companyId && s.Supplier.IsActive == true),
        PurchaseAvailability = new MaterialPurchaseAvailabilityInfoDto
        {
            Status = x.PurchaseAvailability == null ? MaterialPurchaseStatus.Available : x.PurchaseAvailability.Status,
            Reason = x.PurchaseAvailability == null ? null : x.PurchaseAvailability.Reason,
            EffectiveFrom = x.PurchaseAvailability == null ? null : x.PurchaseAvailability.EffectiveFrom,
            ExpectedAvailableDate = x.PurchaseAvailability == null ? null : x.PurchaseAvailability.ExpectedAvailableDate
        }
    };

    internal static Expression<Func<Material, PurchasingMaterialDetailDto>> Detail(Guid companyId, bool canViewPrices)
    {
        var selector = DetailWithPrices(companyId);
        return canViewPrices ? selector : (Expression<Func<Material, PurchasingMaterialDetailDto>>)new HideSupplierPrices().Visit(selector)!;
    }

    private sealed class HideSupplierPrices : ExpressionVisitor
    {
        protected override MemberBinding VisitMemberBinding(MemberBinding node)
            => node.Member.DeclaringType == typeof(PurchasingMaterialSupplierDto) && node.Member.Name == nameof(PurchasingMaterialSupplierDto.CurrentPrice)
                ? Expression.Bind(node.Member, Expression.Constant(null, typeof(decimal?)))
                : base.VisitMemberBinding(node);
    }

    private static Expression<Func<Material, PurchasingMaterialDetailDto>> DetailWithPrices(Guid companyId) => x => new()
    {
        MaterialId = x.MaterialId, ExternalId = x.ExternalId, CustomCode = x.CustomCode, Name = x.Name,
        CategoryId = x.CategoryId, CategoryName = x.Category.CompanyId == companyId ? x.Category.Name : null,
        Unit = x.Unit, Package = x.Package, Weight = x.Weight, MinQuantity = x.MinQuantity,
        Barcode = x.Barcode, Comment = x.Comment, IsActive = x.IsActive == true,
        CreatedDate = x.CreatedDate, CreatedBy = x.CreatedBy, UpdatedDate = x.UpdatedDate, UpdatedBy = x.UpdatedBy,
        SupplierCount = x.MaterialsSuppliers.Count(s => s.IsActive == true && s.Supplier.CompanyId == companyId && s.Supplier.IsActive == true),
        PurchaseAvailabilityNote = x.PurchaseAvailability == null ? null : x.PurchaseAvailability.Note,
        PurchaseAvailability = new MaterialPurchaseAvailabilityInfoDto
        {
            Status = x.PurchaseAvailability == null ? MaterialPurchaseStatus.Available : x.PurchaseAvailability.Status,
            Reason = x.PurchaseAvailability == null ? null : x.PurchaseAvailability.Reason,
            EffectiveFrom = x.PurchaseAvailability == null ? null : x.PurchaseAvailability.EffectiveFrom,
            ExpectedAvailableDate = x.PurchaseAvailability == null ? null : x.PurchaseAvailability.ExpectedAvailableDate
        },
        GroupNames = x.MaterialGroupNames.Where(g => g.IsActive).OrderBy(g => g.MaterialGroupNameText)
            .Select(g => g.MaterialGroupNameText).ToList(),
        Suppliers = x.MaterialsSuppliers.Where(s => s.IsActive == true && s.Supplier.CompanyId == companyId && s.Supplier.IsActive == true)
            .OrderByDescending(s => s.IsPreferred == true).ThenBy(s => s.Supplier.ExternalId).ThenBy(s => s.MaterialsSuppliersId)
            .Select(s => new PurchasingMaterialSupplierDto
            {
                MaterialsSupplierId = s.MaterialsSuppliersId, SupplierId = s.SupplierId,
                SupplierCode = s.Supplier.ExternalId, SupplierName = s.Supplier.SupplierName,
                CurrentPrice = s.CurrentPrice, Currency = s.Currency, IsPreferred = s.IsPreferred == true,
                MinDeliveryDays = s.MinDeliveryDays, CreatedDate = s.CreateDate, UpdatedDate = s.UpdatedDate
            }).ToList()
    };

    internal static void NormalizeAvailability(PurchasingMaterialSummaryDto item)
    {
        var value = item.PurchaseAvailability;
        item.PurchaseAvailability = MaterialPurchaseAvailabilityRules.Resolve(value.Status, value.Reason, value.EffectiveFrom, value.ExpectedAvailableDate);
    }
}
