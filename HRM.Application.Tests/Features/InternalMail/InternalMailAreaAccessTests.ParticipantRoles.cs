using System.Text.Json;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Identity;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("Developer", true)]
    [InlineData("President", true)]
    [InlineData("SaleUser", true)]
    [InlineData("LabUser", true)]
    [InlineData("President", false)]
    public async Task BatchedParticipantRolesPreserveStandalonePrivacyLeadersAndReaderGroups(string role, bool active)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var memory = (MemoryStore)(object)store.Db;
        var source = ObserverParticipantFixture(memory, user.CompanyId!.Value);
        var ordinary = store.Db.Employees.Single(e => e.EmployeeId == source[1].EmployeeId);
        ordinary.IsActive = active;
        var account = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = ordinary.EmployeeId };
        var roles = store.Db.Roles.ToList();
        var memberships = store.Db.UserRoles.ToList();
        foreach (var name in new[] { "Leader", "LabUser" })
        {
            var assigned = new ApplicationRole { Id = Guid.NewGuid(), Name = name, NormalizedName = name.ToUpperInvariant() };
            roles.Add(assigned);
            memberships.Add(new() { UserId = account.Id, RoleId = assigned.Id, IsActive = true });
        }
        memory.Values["Users"] = new MemorySet<ApplicationUser>(store.Db.Users.ToList().Append(account));
        memory.Values["Roles"] = new MemorySet<ApplicationRole>(roles);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>(memberships);
        var access = new InternalMailAreaAccessService(store.Db, user);
        InternalConversationParticipantDto[] Clone() => JsonSerializer.Deserialize<InternalConversationParticipantDto[]>(JsonSerializer.Serialize(source))!;
        var expected = await access.WithReaderGroupsAsync(await access.VisibleParticipantsAsync(InternalMailRelatedType.Internal, Clone(), default), default);
        var snapshot = await access.ParticipantRolesAsync(source.Select(p => p.EmployeeId), default);
        var actual = await access.WithReaderGroupsAsync(await access.VisibleParticipantsAsync(InternalMailRelatedType.Internal, Clone(), default, snapshot), default, snapshot);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(active, snapshot.Leaders.Contains(ordinary.EmployeeId));
        Assert.Equal(active, snapshot.LabUsers.Contains(ordinary.EmployeeId));
    }
}
