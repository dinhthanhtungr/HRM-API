using HRM.Domain.Entities.CustomerSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.CRM.CustomerCare;

public sealed class CustomerSampleSharingModelTests
{
    [Fact]
    public void ModelRequiresUniqueActiveMembershipAndCompanyScopedGroupForeignKey()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var member = db.Model.FindEntityType(typeof(CustomerSampleSharingMember))!;
        var unique = Assert.Single(member.GetIndexes().Where(x => x.IsUnique));
        Assert.Equal(new[] { "CompanyId", "CustomerId" }, unique.Properties.Select(x => x.Name));
        Assert.Equal("\"IsActive\" = TRUE", unique.GetFilter());
        Assert.Contains(member.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(CustomerSampleSharingGroup)
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "GroupId" }));
    }
}
