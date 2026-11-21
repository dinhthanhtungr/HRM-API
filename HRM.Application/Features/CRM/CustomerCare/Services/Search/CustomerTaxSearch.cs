using System.Linq.Expressions;
using HRM.Application.Commons.Searching;

namespace HRM.Application.Features.CRM.CustomerCare.Services.Search;

/// <summary>Một định nghĩa MST KH cho các schema; caller vẫn sở hữu query/visibility scope.</summary>
internal static class CustomerTaxSearch
{
    private static readonly string[] Aliases =
        ["MST", "MST KH", "Mã số thuế", "Mã số thuế KH", "Mã số thuế khách hàng", "Tax number", "Tax code", "Customer tax number", "Customer tax code"];

    internal static SmartSearchField<T> CustomerTaxNumber<T>(this SmartSearchFieldFactory<T> fields,
        Expression<Func<T, string?>> value) => fields.Code("customerTaxNumber", value, Aliases);

    internal static SmartSearchField<T> CustomerTaxNumbers<T, TChild>(this SmartSearchFieldFactory<T> fields,
        Expression<Func<T, IEnumerable<TChild>>> children, Expression<Func<TChild, string?>> value)
        => fields.Codes("customerTaxNumber", children, value, Aliases);
}
