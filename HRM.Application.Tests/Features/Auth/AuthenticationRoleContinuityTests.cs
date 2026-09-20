using System.IdentityModel.Tokens.Jwt;
using HRM.Application.Abstractions.Authentication;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Auth.Contracts;
using HRM.Application.Features.Auth.UseCases.Login;
using HRM.Application.Features.Auth.UseCases.RefreshToken;
using HRM.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HRM.Application.Tests.Features.Auth;

public sealed class AuthenticationRoleContinuityTests
{
    [Fact]
    public async Task Login_PreservesDeveloperRoleInTokenInputAndResponse()
    {
        var identity = new StubIdentityAuthenticationService
        {
            User = DeveloperUser(),
            LoginSession = new RefreshTokenSessionDto(
                "shared-refresh-token",
                DateTime.UtcNow.AddDays(7))
        };
        var tokens = new RecordingTokenService();
        var handler = new LoginCommandHandler(identity, tokens);

        var result = await handler.Handle(
            new LoginCommand("developer", "password"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(ApplicationRoles.Developer, result.Roles);
        Assert.Contains(ApplicationRoles.Developer, tokens.AccessTokenUser!.Roles);
        Assert.Equal(1, identity.StoreOrReuseCalls);
        Assert.Equal("shared-refresh-token", result.RefreshToken);
    }

    [Fact]
    public async Task Refresh_PreservesDeveloperRoleAfterAtomicRotation()
    {
        var identity = new StubIdentityAuthenticationService
        {
            User = DeveloperUser(),
            RenewSession = new RefreshTokenSessionDto(
                "new-refresh-token",
                DateTime.UtcNow.AddDays(7))
        };
        var tokens = new RecordingTokenService();
        var handler = new RefreshTokenCommandHandler(identity, tokens);

        var result = await handler.Handle(
            new RefreshTokenCommand { RefreshToken = "current-refresh-token" },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(ApplicationRoles.Developer, result.Roles);
        Assert.Contains(ApplicationRoles.Developer, tokens.AccessTokenUser!.Roles);
        Assert.Equal("current-refresh-token", identity.ExpectedRefreshToken);
        Assert.Equal(1, identity.RenewCalls);
    }

    [Fact]
    public async Task Refresh_WhenTokenWasAlreadyRotated_DoesNotIssueAccessToken()
    {
        var identity = new StubIdentityAuthenticationService
        {
            User = DeveloperUser(),
            RenewSession = null
        };
        var tokens = new RecordingTokenService();
        var handler = new RefreshTokenCommandHandler(identity, tokens);

        var result = await handler.Handle(
            new RefreshTokenCommand { RefreshToken = "consumed-refresh-token" },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, identity.RenewCalls);
        Assert.Equal(0, tokens.AccessTokenCalls);
    }

    [Fact]
    public async Task Refresh_ReturnsSharedSessionSelectedByIdentityService()
    {
        var sharedExpiresAt = DateTime.UtcNow.AddDays(3);
        var identity = new StubIdentityAuthenticationService
        {
            User = DeveloperUser(),
            RenewSession = new RefreshTokenSessionDto(
                "current-refresh-token",
                sharedExpiresAt)
        };
        var handler = new RefreshTokenCommandHandler(identity, new RecordingTokenService());

        var result = await handler.Handle(
            new RefreshTokenCommand { RefreshToken = "current-refresh-token" },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("current-refresh-token", result.RefreshToken);
        Assert.Equal(sharedExpiresAt, result.RefreshTokenExpireAtUtc);
    }

    [Fact]
    public void JwtToken_ContainsDeveloperInCanonicalRolesClaim()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "authentication-role-continuity-test-signing-key-1234567890",
                ["Jwt:Issuer"] = "HRM.Tests",
                ["Jwt:Audience"] = "HRM.Tests",
                ["Jwt:EXPIRATION_MINUTES"] = "30"
            })
            .Build();
        var tokenService = new JwtTokenService(configuration);

        var accessToken = tokenService.CreateAccessToken(DeveloperUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Token);

        Assert.Contains(
            jwt.Claims,
            claim => claim.Type == "roles" && claim.Value == ApplicationRoles.Developer);
    }

    [Fact]
    public void JwtToken_ContainsDatabasePermissionClaimsAndModelMarker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "authentication-permission-continuity-test-signing-key-1234567890",
                ["Jwt:Issuer"] = "HRM.Tests",
                ["Jwt:Audience"] = "HRM.Tests"
            })
            .Build();
        var tokenService = new JwtTokenService(configuration);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            tokenService.CreateAccessToken(DeveloperUser()).Token);

        Assert.Contains(jwt.Claims, claim =>
            claim.Type == ApplicationPermissionClaimTypes.Permission &&
            claim.Value == ApplicationPermissions.Pricing.Manage);
        Assert.Contains(jwt.Claims, claim =>
            claim.Type == ApplicationPermissionClaimTypes.PermissionModelVersion &&
            claim.Value == ApplicationPermissionClaimTypes.CurrentModelVersion);
    }

    private static AuthenticatedUserDto DeveloperUser() => new()
    {
        UserId = Guid.NewGuid(),
        UserName = "developer",
        Email = "developer@example.test",
        EmployeeId = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        Roles = [ApplicationRoles.Developer],
        Permissions = [ApplicationPermissions.Pricing.Manage],
        UsesDatabasePermissions = true
    };

    private sealed class RecordingTokenService : ITokenService
    {
        public int AccessTokenCalls { get; private set; }
        public AuthenticatedUserDto? AccessTokenUser { get; private set; }

        public AccessTokenDto CreateAccessToken(AuthenticatedUserDto user)
        {
            AccessTokenCalls++;
            AccessTokenUser = user;
            return new AccessTokenDto
            {
                Token = "access-token",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
            };
        }

        public string CreateRefreshToken() => "new-refresh-token";
    }

    private sealed class StubIdentityAuthenticationService : IIdentityAuthenticationService
    {
        public AuthenticatedUserDto? User { get; init; }
        public RefreshTokenSessionDto? LoginSession { get; init; }
        public RefreshTokenSessionDto? RenewSession { get; init; }
        public int StoreOrReuseCalls { get; private set; }
        public int RenewCalls { get; private set; }
        public string? ExpectedRefreshToken { get; private set; }

        public Task<AuthenticatedUserDto?> ValidateUserAsync(
            string userNameOrEmail,
            string password,
            CancellationToken cancellationToken = default) => Task.FromResult(User);

        public Task<RefreshTokenSessionDto> StoreOrReuseRefreshTokenAsync(
            Guid userId,
            string proposedRefreshToken,
            DateTime proposedExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            StoreOrReuseCalls++;
            return Task.FromResult(LoginSession ?? new RefreshTokenSessionDto(
                proposedRefreshToken,
                proposedExpiresAtUtc));
        }

        public Task<AuthenticatedUserDto?> ValidateRefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default) => Task.FromResult(User);

        public Task<RefreshTokenSessionDto?> RenewRefreshTokenAsync(
            Guid userId,
            string expectedRefreshToken,
            string proposedRefreshToken,
            DateTime proposedExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            RenewCalls++;
            ExpectedRefreshToken = expectedRefreshToken;
            return Task.FromResult(RenewSession);
        }

        public Task RevokeRefreshTokenAsync(
            Guid userId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
