using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalMessageDeletionAccessTests
{
    [Theory]
    [InlineData("Developer", true)]
    [InlineData("Admin", false)]
    [InlineData("President", false)]
    [InlineData("SaleUser", false)]
    public void OnlyDeveloperCanDelete_EvenWhenSenderAndOwner(string role, bool allowed)
    {
        var user = new User(role);
        var message = Message(user.CompanyId!.Value);
        message.SenderEmployeeId = user.EmployeeId!.Value;
        message.Conversation.Participants.Add(new()
        {
            EmployeeId = user.EmployeeId.Value, IsActive = true,
            Role = InternalConversationParticipantRole.Owner
        });
        Assert.Equal(allowed, Access(user).Scope(new[] { message }.AsQueryable()).Any());
    }

    [Fact]
    public void DeveloperCanDeleteOthersMessagesWithoutMembership_InEveryAreaAndMessageType()
    {
        var user = new User("Developer");
        foreach (var area in Enum.GetValues<InternalMailRelatedType>())
        foreach (var type in Enum.GetValues<InternalMessageType>())
        {
            var message = Message(user.CompanyId!.Value);
            message.Conversation.RelatedType = area;
            message.MessageType = type;
            Assert.NotEqual(user.EmployeeId, message.SenderEmployeeId);
            Assert.Empty(message.Conversation.Participants);
            Assert.Single(Access(user).Scope(new[] { message }.AsQueryable()));
        }
    }

    [Fact]
    public void ScopeRejectsOtherCompany_InactiveConversation_AndDeletedMessage()
    {
        var user = new User("Developer");
        var own = Message(user.CompanyId!.Value);
        var other = Message(Guid.NewGuid());
        var inactive = Message(user.CompanyId.Value);
        inactive.Conversation.IsActive = false;
        var deleted = Message(user.CompanyId.Value);
        deleted.IsDeleted = true;
        Assert.Same(own, Assert.Single(Access(user).Scope(new[] { own, other, inactive, deleted }.AsQueryable())));
    }

    [Fact]
    public void FailsClosedForMissingContextAndRevokedPermission()
    {
        var user = new User("Developer") { IsAuthenticated = false };
        Assert.False(Access(user).CanDelete);
        user.IsAuthenticated = true;
        user.CompanyId = Guid.Empty;
        Assert.False(Access(user).CanDelete);
        user.CompanyId = null;
        Assert.False(Access(user).CanDelete);
        user.CompanyId = Guid.NewGuid();
        user.EmployeeId = null;
        Assert.False(Access(user).CanDelete);
        user.EmployeeId = Guid.NewGuid();
        user.HasExplicitPermissionSet = true;
        Assert.False(Access(user).CanDelete);
        user.Permissions = [ApplicationPermissions.InternalMail.DeleteMessage];
        Assert.True(Access(user).CanDelete);
        Assert.False(new CurrentUserPermissionService(user).HasPermission("unknown.permission"));
    }

    [Fact]
    public void ExplicitPermissionCannotGiveNonDeveloperModeration()
    {
        var user = new User("SaleUser")
        {
            HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.InternalMail.DeleteMessage]
        };
        Assert.False(Access(user).CanDelete);
    }

    private static InternalMessageDeletionAccess Access(User user) => new(user, new CurrentUserPermissionService(user));
    private static InternalMessage Message(Guid company) => new()
    {
        InternalMessageId = Guid.NewGuid(), SenderEmployeeId = Guid.NewGuid(),
        Conversation = new() { CompanyId = company, IsActive = true }
    };

    private sealed class User(string role) : ICurrentUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; set; } = Guid.NewGuid();
        public Guid? CompanyId { get; set; } = Guid.NewGuid();
        public string? UserName => "moderation-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public IReadOnlyCollection<string> Permissions { get; set; } = [];
        public bool HasExplicitPermissionSet { get; set; }
        public bool IsInRole(string name) => string.Equals(role, name, StringComparison.OrdinalIgnoreCase);
    }
}
