using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Commons.Searching;

/// <summary>Expression của field do feature khai báo; không tự lấy dữ liệu hoặc mở rộng quyền.</summary>
internal sealed record SmartSearchField<T>(string Key, string[] Aliases, bool IsName,
    Func<IQueryable<T>, IQueryable<string>> Values,
    Func<string[], Expression<Func<T, bool>>> EqualsAny,
    Func<string, Expression<Func<T, bool>>> Matches)
{
    internal static SmartSearchField<T> Text(string key, bool isName, Expression<Func<T, string?>> value,
        params string[] aliases)
    {
        var selector = Expression.Lambda<Func<T, string>>(Expression.Coalesce(value.Body, Expression.Constant("")), value.Parameters);
        return new(key, aliases, isName, rows => rows.Select(selector),
            values => Expression.Lambda<Func<T, bool>>(Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains),
                [typeof(string)], Expression.Constant(values), selector.Body), selector.Parameters),
            keyword => Expression.Lambda<Func<T, bool>>(Replace(MatchValue(isName, keyword), selector.Body), selector.Parameters));
    }

    internal static SmartSearchField<T> Many<TChild>(string key, bool isName,
        Expression<Func<T, IEnumerable<TChild>>> children, Expression<Func<TChild, string?>> value,
        params string[] aliases)
    {
        var child = SmartSearchField<TChild>.Text(key, isName, value, aliases);
        Expression<Func<T, bool>> Any(Expression<Func<TChild, bool>> predicate) =>
            Expression.Lambda<Func<T, bool>>(Expression.Call(typeof(Enumerable), nameof(Enumerable.Any),
                [typeof(TChild)], children.Body, predicate), children.Parameters);
        return new(key, aliases, isName, rows => child.Values(rows.SelectMany(children)),
            values => Any(child.EqualsAny(values)), keyword => Any(child.Matches(keyword)));
    }

    private static Expression<Func<string, bool>> MatchValue(bool isName, string keyword)
    {
        if (isName)
        {
            var pattern = SearchNameText.AllWordsPattern(keyword);
            return value => Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase);
        }
        var literal = PostgresSearchPattern.ContainsLiteral(keyword);
        return value => EF.Functions.ILike(value, literal, PostgresSearchPattern.EscapeCharacter);
    }

    internal static Expression Replace(LambdaExpression expression, Expression value)
        => new ParameterSubstitution(expression.Parameters[0], value).Visit(expression.Body)!;

    private sealed class ParameterSubstitution(ParameterExpression parameter, Expression value) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? value : base.VisitParameter(node);
    }
}
