using HRM.Application.Features.InternalMail.Queries.SearchGroupMembers;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("SaleUser", false, true, true)]
    [InlineData("SaleUser", true, false, true)]
    [InlineData("SaleUser", true, true, false)]
    [InlineData("President", true, false, true)]
    public async Task CompanyMemberLookupRequiresSourceAccessAndSampleRequest(string role, bool sourceMember, bool sameCompany, bool sample)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.Internal, false);
        store.Root.RelatedType = sample ? InternalMailRelatedType.SampleRequest : InternalMailRelatedType.Internal;
        if (sourceMember) store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        if (!sameCompany) store.Root.CompanyId = Guid.NewGuid();
        var handler = new SearchInternalMailGroupMembersQueryHandler(store.Db,user,new InternalMailAreaAccessService(store.Db,user));
        Assert.Null(await handler.Handle(new(store.Root.InternalConversationId,"Tùng"),default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("T")]
    [InlineData("Tùng%_")]
    public void CompanyLookupIsBoundedAndTranslatesWithPrivacyScopeAndLiteralKeyword(string keyword)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("SaleUser");
        var handler = new SearchInternalMailGroupMembersQueryHandler(db,user,new InternalMailAreaAccessService(db,user));
        var sql = handler.Candidates(Guid.NewGuid(),keyword).ToQueryString();
        if (keyword.Length > 0) Assert.Contains("ILIKE",sql);
        else Assert.DoesNotContain("ILIKE",sql);
        Assert.Contains("LIMIT",sql);
        Assert.Contains("CompanyId",sql);
        Assert.Contains("IsActive",sql);
        Assert.Contains("AspNetUserRoles",sql);
        if (keyword.Contains('%')) Assert.Contains(@"\%\_",sql);
    }
}
