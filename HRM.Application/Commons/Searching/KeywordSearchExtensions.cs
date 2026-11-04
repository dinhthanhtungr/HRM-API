using System.Linq.Expressions;

namespace HRM.Application.Commons.Searching;

/// <summary>Áp dụng keyword bằng expression để provider dịch SQL, không tải dữ liệu về lọc.</summary>
public static class KeywordSearchExtensions
{
    public static IQueryable<T> WhereKeyword<T>(
        this IQueryable<T> query,
        string? keyword,
        Func<KeywordSearchTerm, Expression<Func<T, bool>>> predicateFactory)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicateFactory);
        if (string.IsNullOrWhiteSpace(keyword)) return query;

        var term = new KeywordSearchTerm(keyword.Trim());
        return query.Where(predicateFactory(term));
    }
}
