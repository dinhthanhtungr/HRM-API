using System.Reflection;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Notifications.Commands.SendTestNotification;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.Notifications;

public sealed class SendTestNotificationTests
{
    [Theory]
    [InlineData(true, ApplicationRoles.Developer, true, true, true)]
    [InlineData(true, ApplicationRoles.User, true, true, false)]
    [InlineData(true, ApplicationRoles.President, true, true, false)]
    [InlineData(false, ApplicationRoles.Developer, true, true, false)]
    [InlineData(true, ApplicationRoles.Developer, false, true, false)]
    [InlineData(true, ApplicationRoles.Developer, true, false, false)]
    public async Task Publish_RequiresDeveloperAndValidContext(
        bool authenticated, string role, bool hasCompany, bool hasEmployee, bool expected)
    {
        var companyId = hasCompany ? Guid.NewGuid() : Guid.Empty;
        var employeeId = hasEmployee ? Guid.NewGuid() : Guid.Empty;
        var user = new TestUser(authenticated, role, companyId, employeeId);
        var service = DispatchProxy.Create<INotificationService, PublishSpy>();
        var spy = (PublishSpy)(object)service;
        var handler = new SendTestNotificationCommandHandler(user, service);

        var result = await handler.Handle(new SendTestNotificationCommand(), CancellationToken.None);

        Assert.Equal(expected, result.Success);
        if (!expected)
        {
            Assert.Null(spy.Request);
            return;
        }

        var request = Assert.IsType<PublishNotificationRequest>(spy.Request);
        Assert.Equal(companyId, request.CompanyId);
        Assert.Equal(employeeId, request.CreatedBy);
        Assert.Equal(TopicNotifications.DeveloperNotificationTest, request.Topic);
        Assert.Null(request.TargetUserIds);
        Assert.Null(request.TargetRoles);
        Assert.Null(request.TargetTeamIds);
        Assert.Null(request.SilentUserIds);
        Assert.Equal("system", NotificationTopicCatalog.GetDefinition(request.Topic).CategoryCode);
    }

    public class PublishSpy : DispatchProxy
    {
        public PublishNotificationRequest? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal(nameof(INotificationService.PublishAsync), targetMethod?.Name);
            Request = Assert.IsType<PublishNotificationRequest>(args![0]);
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed record TestUser(bool IsAuthenticated, string Role, Guid Company, Guid Employee) : ICurrentUser
    {
        public Guid UserId => Guid.NewGuid();
        public Guid? CompanyId => Company;
        public Guid? EmployeeId => Employee;
        public string? UserName => "Developer test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => role == Role;
    }
}
