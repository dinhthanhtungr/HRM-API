using System.Linq.Expressions;
using HRM.Application.Commons.Searching;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines;

/// <summary>Chọn field tìm kiếm cho danh sách PO giao hàng; không thay thế scope của query gốc.</summary>
internal static class SelectableDeliveryKeywordSearch
{
    internal static IQueryable<SelectableDeliveryLineSource> Apply(
        IQueryable<SelectableDeliveryLineSource> query, string? keyword, Guid companyId)
        => query.WhereKeyword(keyword, term => BuildPredicate(term, companyId));

    private static Expression<Func<SelectableDeliveryLineSource, bool>> BuildPredicate(KeywordSearchTerm term, Guid companyId)
    {
        var pattern = term.ContainsPattern;
        // Chọn nhánh trước khi dựng SQL để mã định danh không tìm qua mọi field.
        if (term.StartsWith("DHG"))
        {
            return x =>
                EF.Functions.ILike(x.Order.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter);
        }
        else if (term.StartsWith("TP_"))
        {
            return x =>
                x.Detail.Product.IsActive && x.Detail.Product.CompanyId == companyId &&
                x.Detail.Product.SampleRequests.Any(sr => sr.IsActive && sr.CompanyId == companyId &&
                    EF.Functions.ILike(sr.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter));
        }
        else if (term.StartsWithAsciiLetters(2))
        {
            return x =>
                x.Detail.Product.IsActive && x.Detail.Product.CompanyId == companyId &&
                (EF.Functions.ILike(x.Detail.Product.ColourCode ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                 x.Detail.Product.SampleRequests.Any(sr => sr.IsActive && sr.CompanyId == companyId &&
                     EF.Functions.ILike(sr.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter)));
        }
        else
        {
            return x =>
                EF.Functions.ILike(x.Order.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Order.PONo, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Order.CustomerNameSnapshot, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Order.PhoneSnapshot, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Detail.ProductExternalIdSnapshot, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Detail.ProductNameSnapshot, pattern, PostgresSearchPattern.EscapeCharacter) ||
                (x.Detail.Product != null && x.Detail.Product.CompanyId == companyId &&
                    (EF.Functions.ILike(x.Detail.Product.ColourCode ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                     EF.Functions.ILike(x.Detail.Product.Name ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                     x.Detail.Product.SampleRequests.Any(sr => sr.IsActive && sr.CompanyId == companyId &&
                         EF.Functions.ILike(sr.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter)))) ||
                EF.Functions.ILike((x.Detail.Comment ?? string.Empty), pattern, PostgresSearchPattern.EscapeCharacter);
        }
    }
}
