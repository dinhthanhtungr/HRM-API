using System.Text.RegularExpressions;
using HRM.Application.Features.PLM.SampleRequests.Services.Search;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;

/// <summary>Chỉ đọc metadata trong tập Summary đã scope; mỗi loại có giới hạn riêng trước UNION ALL.</summary>
internal static class SummarySuggestionQueries
{
    internal static IQueryable<Candidate> Build(IPLMReadDbContext db, IQueryable<SampleRequest> visible, string keyword)
        => Build(db, visible, SampleRequestSearchInput.Parse(keyword));

    internal static IQueryable<Candidate> Build(IPLMReadDbContext db, IQueryable<SampleRequest> visible, SampleRequestSearchInput input)
    {
        var keyword = input.Keyword;
        var pattern = SampleRequestSearchText.PhrasePattern(keyword);
        var groupsKeyword = SampleRequestSearchText.Fold(keyword) is "nhom" or "group";
        IQueryable<Candidate>? combined = null;
        bool Includes(string type) => input.Type is null || input.Type == type;
        void Add(IQueryable<Candidate> branch) => combined = combined is null ? branch : combined.Concat(branch);

        if (Includes("employee"))
            Add(MatchText(db.Employees.AsNoTracking().Where(e => e.IsActive &&
                    visible.Any(x => e.CompanyId == x.CompanyId &&
                        (x.ManagerBy == e.EmployeeId || x.CreatedBy == e.EmployeeId || x.Product.CreatedBy == e.EmployeeId)))
                .Select(e => new Candidate { Type = "employee", Value = e.EmployeeId.ToString(),
                    Label = e.FullName, Code = e.ExternalId }), keyword, pattern));
        if (Includes("group"))
            Add(MatchText(db.CustomerAssignments.AsNoTracking().Where(a => a.IsActive &&
                    a.Group.CompanyId == a.CompanyId &&
                    visible.Any(x => x.CompanyId == a.CompanyId && x.CustomerId == a.CustomerId))
                .Select(a => new Candidate { Type = "group", Value = a.GroupId.ToString(),
                    Label = a.Group.Name ?? a.Group.ExternalId, Code = a.Group.ExternalId }).Distinct(),
                keyword, groupsKeyword && input.Type is null ? "" : pattern));
        if (Includes("formula"))
            Add(MatchCode(db.Formulas.AsNoTracking().Where(f => f.IsActive &&
                    visible.Any(x => x.CompanyId == f.CompanyId && x.ProductId == f.ProductId))
                .Select(f => new Candidate { Type = "formula", Value = f.FormulaId.ToString(),
                    Label = f.ExternalId, Code = "" }), keyword));
        if (Includes("colourCode"))
            Add(MatchCode(visible.Where(x => x.Product.ColourCode != null && x.Product.ColourCode != "")
                .Select(x => new Candidate { Type = "colourCode", Value = x.Product.ColourCode!,
                    Label = x.Product.ColourCode!, Code = "" }).Distinct(), keyword));
        if (Includes("customer"))
            Add(MatchText(db.Customers.AsNoTracking().Where(c =>
                    visible.Any(x => x.CompanyId == c.CompanyId && x.CustomerId == c.CustomerId))
                .Select(c => new Candidate { Type = "customer", Value = c.CustomerId.ToString(),
                    Label = c.CustomerName, Code = c.ExternalId }), keyword, pattern));
        if (Includes("customerCode"))
            Add(MatchCode(db.Customers.AsNoTracking().Where(c =>
                    visible.Any(x => x.CompanyId == c.CompanyId && x.CustomerId == c.CustomerId))
                .Select(c => new Candidate { Type = "customerCode", Value = c.CustomerId.ToString(),
                    Label = c.ExternalId, Code = "" }), keyword));
        if (Includes("customerTaxNumber"))
            Add(MatchCode(db.Customers.AsNoTracking().Where(c => c.TaxNumber != null && c.TaxNumber != "" &&
                    visible.Any(x => x.CompanyId == c.CompanyId && x.CustomerId == c.CustomerId))
                .Select(c => new Candidate { Type = "customerTaxNumber", Value = c.CustomerId.ToString(),
                    Label = c.TaxNumber!, Code = "" }), keyword));
        if (Includes("product"))
            Add(MatchText(db.Products.AsNoTracking().Where(p => p.IsActive && p.Name != null && p.Name != "" &&
                    visible.Any(x => x.CompanyId == p.CompanyId && x.ProductId == p.ProductId))
                .Select(p => new Candidate { Type = "product", Value = p.ProductId.ToString(),
                    Label = p.Name!, Code = "" }), keyword, pattern));
        if (Includes("sampleRequest"))
            Add(MatchCode(visible.Select(x => new Candidate { Type = "sampleRequest",
                Value = x.SampleRequestId.ToString(), Label = x.ExternalId, Code = "" }), keyword));
        if (Includes("endUser"))
            Add(MatchText(visible.Where(x => x.Product.IsActive && x.Product.EndUser != null &&
                    x.Product.EndUser != "" && x.Product.EndUser.Length <= 100)
                .Select(x => new Candidate { Type = "endUser", Value = x.Product.EndUser!,
                    Label = x.Product.EndUser!, Code = "" }).Distinct(), keyword, pattern));

        return combined ?? visible.Where(x => false).Select(x =>
            new Candidate { Value = "", Label = "", Type = "", Code = "" });
    }

    internal static IQueryable<Candidate> MatchText(IQueryable<Candidate> query, string keyword, string pattern)
    {
        var allWordsPattern = pattern.Length == 0 ? "" : SampleRequestSearchText.AllWordsPattern(keyword);
        var orderedPattern = SampleRequestSearchText.OrderedPattern(keyword);
        // Một từ đã được WHERE xác nhận tồn tại; chỉ nhiều từ mới có thứ tự để phân biệt.
        var needsOrderCheck = SampleRequestSearchText.Words(keyword).Length > 1;
        return query.WhereKeyword(allWordsPattern, term => x =>
                Regex.IsMatch(x.Label, term.Value, RegexOptions.IgnoreCase))
            .Select(x => new Candidate
            {
                Type = x.Type, Value = x.Value, Label = x.Label, Code = x.Code,
                MatchRank = Regex.IsMatch(x.Label, "^" + pattern + "$", RegexOptions.IgnoreCase) ? 0 :
                    Regex.IsMatch(x.Label, "^" + pattern, RegexOptions.IgnoreCase) ? 1 :
                    !needsOrderCheck || Regex.IsMatch(x.Label, orderedPattern, RegexOptions.IgnoreCase) ? 2 : 3
            })
            .OrderBy(x => x.MatchRank).ThenBy(x => x.Label).ThenBy(x => x.Value)
            .Take(SummarySuggestionSelection.MaximumItems);
    }

    internal static IQueryable<Candidate> MatchCode(IQueryable<Candidate> query, string keyword)
    {
        var exact = PostgresSearchPattern.ExactLiteral(keyword);
        var prefix = PostgresSearchPattern.PrefixLiteral(keyword);
        return query.WhereKeyword(keyword, term => x =>
                EF.Functions.ILike(x.Label, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter))
            .Select(x => new Candidate
            {
                Type = x.Type, Value = x.Value, Label = x.Label, Code = x.Code,
                MatchRank = EF.Functions.ILike(x.Label, exact, PostgresSearchPattern.EscapeCharacter) ? 0 :
                    EF.Functions.ILike(x.Label, prefix, PostgresSearchPattern.EscapeCharacter) ? 1 : 2
            })
            .OrderBy(x => x.MatchRank).ThenBy(x => x.Label).ThenBy(x => x.Value)
            .Take(SummarySuggestionSelection.MaximumItems);
    }

    internal sealed class Candidate
    {
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
        public string Code { get; set; } = "";
        public int MatchRank { get; set; }
    }
}
