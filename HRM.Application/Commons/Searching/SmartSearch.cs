using System.Linq.Expressions;

namespace HRM.Application.Commons.Searching;

internal static class SmartSearch
{
    internal static SmartSearchSchema<T> For<T>(IQueryable<T> scoped,
        Func<SmartSearchFieldFactory<T>, SmartSearchField<T>[]> configure) => new(configure(new()));
}

internal sealed class SmartSearchFieldFactory<T>
{
    internal SmartSearchField<T> Name(string key, Expression<Func<T, string?>> value, params string[] aliases)
        => SmartSearchField<T>.Text(key, true, value, aliases);
    internal SmartSearchField<T> Code(string key, Expression<Func<T, string?>> value, params string[] aliases)
        => SmartSearchField<T>.Text(key, false, value, aliases);
    internal SmartSearchField<T> Names<TChild>(string key, Expression<Func<T, IEnumerable<TChild>>> children,
        Expression<Func<TChild, string?>> value, params string[] aliases)
        => SmartSearchField<T>.Many(key, true, children, value, aliases);
    internal SmartSearchField<T> Codes<TChild>(string key, Expression<Func<T, IEnumerable<TChild>>> children,
        Expression<Func<TChild, string?>> value, params string[] aliases)
        => SmartSearchField<T>.Many(key, false, children, value, aliases);
}
