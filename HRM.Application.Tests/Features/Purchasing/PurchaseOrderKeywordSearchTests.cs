using System.Linq.Expressions;
using System.Text.RegularExpressions;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderKeywordSearchTests
{
    [Theory]
    [InlineData("DHG261000233")]
    [InlineData("NVL_NH_137")]
    [InlineData("Hạt nhựa")]
    public void InactiveLinksAndLinesAreSearchableOnlyWithFlag(string keyword)
    {
        var company = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            CompanyId = company, ExternalId = "DDH_1", IsActive = true, Status = "Canceled",
            PurchaseOrderDetails = [new() { IsActive = false, MaterialExternalIDSnapshot = "NVL_NH_137", MaterialNameSnapshot = "Hạt nhựa" }],
            PurchaseOrderLinks = [new() { IsActive = false, MerchandiseOrder = new() { ExternalId = "DHG261000233", CompanyId = company } }]
        };
        Assert.Empty(Filter([po], company, keyword, false));
        Assert.Same(po, Assert.Single(Filter([po], company, keyword, true)));
    }

    [Fact]
    public void ScopeAndLiteralSearchRemainIntact_AndPoCodeCanFindInactiveParent()
    {
        var company = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            CompanyId = company, ExternalId = "DDH_%_1", IsActive = false,
            PurchaseOrderLinks = [new() { IsActive = false, MerchandiseOrder = new() { ExternalId = "DHG_FOREIGN", CompanyId = Guid.NewGuid() } }]
        };
        Assert.Single(Filter([po], company, " ddh_% ", true));
        Assert.Empty(Filter([po], company, "DDH_%", false));
        Assert.Empty(Filter([po], company, "DHG_FOREIGN", true));
        Assert.Empty(Filter([po], Guid.NewGuid(), "DDH", true));
        Assert.Empty(Filter([po], company, "DDH_X", true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeywordFilterTranslatesToPostgresBeforeCountAndPaging(bool includeInactive)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var query = PurchaseOrderKeywordSearch.Apply(db.PurchaseOrders.Where(x => x.CompanyId == company &&
            (includeInactive || x.IsActive == true)), "DHG261000233", company, includeInactive);
        var sql = query.OrderBy(x => x.PurchaseOrderId).Skip(15).Take(15).ToQueryString();
        Assert.Contains("ILIKE", sql);
        Assert.Contains("EXISTS", sql);
        Assert.Contains(db.Model.FindEntityType(typeof(PurchaseOrderLink))!.GetTableName()!, sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("LIMIT", sql);
    }

    private static List<PurchaseOrder> Filter(PurchaseOrder[] rows, Guid company, string keyword, bool inactive)
    {
        var query = PurchaseOrderKeywordSearch.Apply(rows.AsQueryable().Where(x => x.CompanyId == company &&
            (inactive || x.IsActive == true)), keyword, company, inactive);
        // Emulate only PostgreSQL ILIKE for in-memory fixture evaluation; SQL translation is tested separately.
        return query.Provider.CreateQuery<PurchaseOrder>(new LikeVisitor().Visit(query.Expression)!).ToList();
    }

    public static bool Like(string value, string pattern, string escape)
    {
        var regex = "";
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == escape[0] && i + 1 < pattern.Length) regex += Regex.Escape(pattern[++i].ToString());
            else regex += c == '%' ? ".*" : c == '_' ? "." : Regex.Escape(c.ToString());
        }
        return Regex.IsMatch(value, "^" + regex + "$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    private sealed class LikeVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
            => node.Method.Name == "ILike" ? Expression.Call(typeof(PurchaseOrderKeywordSearchTests).GetMethod(nameof(Like))!,
                Visit(node.Arguments[1]), Visit(node.Arguments[2]), Visit(node.Arguments[3])) : base.VisitMethodCall(node);
    }
}
