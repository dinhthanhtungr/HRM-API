using System.Text.RegularExpressions;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Services.Search;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;

/// <summary>Keyword tự do và nhãn chọn trường có cùng semantics với gợi ý, lọc trước phân trang.</summary>
internal static class SampleRequestSummaryKeywordSearch
{
    internal static IQueryable<SampleRequest> Apply(IQueryable<SampleRequest> query,
        IPLMReadDbContext db, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return query;
        var input = SampleRequestSearchInput.Parse(raw);
        if (input.Keyword.Length == 0) return query.Where(x => false);
        var pattern = SampleRequestSearchText.AllWordsPattern(input.Keyword);
        var literal = PostgresSearchPattern.ContainsLiteral(input.Keyword);
        return input.Type switch
        {
            "customer" => query.Where(x => Regex.IsMatch(x.Customer.CustomerName, pattern, RegexOptions.IgnoreCase)),
            "endUser" => query.Where(x => x.Product.IsActive &&
                Regex.IsMatch(x.Product.EndUser ?? "", pattern, RegexOptions.IgnoreCase)),
            "customerCode" => query.Where(x => EF.Functions.ILike(x.Customer.ExternalId, literal, PostgresSearchPattern.EscapeCharacter)),
            "customerTaxNumber" => query.Where(x => x.Customer.CompanyId == x.CompanyId &&
                EF.Functions.ILike(x.Customer.TaxNumber ?? "", literal, PostgresSearchPattern.EscapeCharacter)),
            "product" => query.Where(x => x.Product.IsActive &&
                Regex.IsMatch(x.Product.Name ?? "", pattern, RegexOptions.IgnoreCase)),
            "sampleRequest" => query.Where(x => EF.Functions.ILike(x.ExternalId, literal, PostgresSearchPattern.EscapeCharacter)),
            "colourCode" => query.Where(x => EF.Functions.ILike(x.Product.ColourCode ?? "", literal, PostgresSearchPattern.EscapeCharacter)),
            "formula" => query.Where(x => x.Product.Formulas.Any(f => f.IsActive && f.CompanyId == x.CompanyId &&
                EF.Functions.ILike(f.ExternalId, literal, PostgresSearchPattern.EscapeCharacter))),
            "employee" => query.Where(x =>
                x.ManagerByNavigation.IsActive && x.ManagerByNavigation.CompanyId == x.CompanyId &&
                    Regex.IsMatch(x.ManagerByNavigation.FullName ?? "", pattern, RegexOptions.IgnoreCase) ||
                x.CreatedByNavigation != null && x.CreatedByNavigation.IsActive && x.CreatedByNavigation.CompanyId == x.CompanyId &&
                    Regex.IsMatch(x.CreatedByNavigation.FullName ?? "", pattern, RegexOptions.IgnoreCase) ||
                x.Product.CreatedByNavigation != null && x.Product.CreatedByNavigation.IsActive &&
                    x.Product.CreatedByNavigation.CompanyId == x.CompanyId &&
                    Regex.IsMatch(x.Product.CreatedByNavigation.FullName ?? "", pattern, RegexOptions.IgnoreCase)),
            "group" => query.Where(x => db.CustomerAssignments.Any(a => a.IsActive && a.CompanyId == x.CompanyId &&
                a.CustomerId == x.CustomerId && a.Group.CompanyId == x.CompanyId &&
                Regex.IsMatch(a.Group.Name ?? a.Group.ExternalId, pattern, RegexOptions.IgnoreCase))),
            _ => query.WhereKeyword(input.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                Regex.IsMatch(x.Customer.CustomerName, pattern, RegexOptions.IgnoreCase) ||
                EF.Functions.ILike(x.Customer.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                x.Customer.CompanyId == x.CompanyId && EF.Functions.ILike(x.Customer.TaxNumber ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                x.CreatedByNavigation != null && Regex.IsMatch(x.CreatedByNavigation.FullName ?? "", pattern, RegexOptions.IgnoreCase) ||
                x.Product.CreatedByNavigation != null && Regex.IsMatch(x.Product.CreatedByNavigation.FullName ?? "", pattern, RegexOptions.IgnoreCase) ||
                Regex.IsMatch(x.Product.Name ?? "", pattern, RegexOptions.IgnoreCase) ||
                Regex.IsMatch(x.Product.EndUser ?? "", pattern, RegexOptions.IgnoreCase) ||
                EF.Functions.ILike(x.Product.ColourCode ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                x.Product.Formulas.Any(f => f.IsActive && f.CompanyId == x.CompanyId &&
                    EF.Functions.ILike(f.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter)))
        };
    }
}
