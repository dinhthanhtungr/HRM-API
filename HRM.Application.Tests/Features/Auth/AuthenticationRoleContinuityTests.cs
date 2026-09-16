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
            User = DeveloperUser()
        };
        var tokens = new RecordingTokenService();
        var handler = new LoginCommandHandler(identity, tokens);

        var result = await handler.Handle(
            new LoginCommand("developer", "password"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(ApplicationRoles.Developer, result.Roles);
        Assert.Contains(ApplicationRoles.Developer, tokens.AccessTokenUser!.Roles);
        Assert.Equal(1, identity.StoreCalls);
    }

    [Fact]
    public async Task Refresh_PreservesDeveloperRoleAfterAtomicRotation()
    {
        var identity = new StubIdentityAuthenticationService
        {
            User = DeveloperUser(),
            RotateResult = true
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
        Assert.Equal(1, identity.RotateCalls);
    }

    [Fact]
    public async Task Refresh_WhenTokenWasAlreadyRotated_DoesNotIssueAccessToken()
    {
        var identity = new StubIdentityAuthenticationService
        {
            User = DeveloperUser(),
            RotateResult = false
        };
        var tokens = new RecordingTokenService();
        var handler = new RefreshTokenCommandHandler(identity, tokens);

        var result = await handler.Handle(
            new RefreshTokenCommand { RefreshToken = "consumed-refresh-token" },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, identity.RotateCalls);
        Assert.Equal(0, tokens.AccessTokenCalls);
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
        public bool RotateResult { get; init; }
        public int StoreCalls { get; private set; }
        public int RotateCalls { get; private set; }
        public string? ExpectedRefreshToken { get; private set; }

        public Task<AuthenticatedUserDto?> ValidateUserAsync(
            string userNameOrEmail,
            string password,
            CancellationToken cancellationToken = default) => Task.FromResult(User);

        public Task StoreRefreshTokenAsync(
            Guid userId,
            string refreshToken,
            DateTime expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            StoreCalls++;
            return Task.CompletedTask;
        }

        public Task<AuthenticatedUserDto?> ValidateRefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default) => Task.FromResult(User);

        public Task<bool> RotateRefreshTokenAsync(
            Guid userId,
            string expectedRefreshToken,
            string newRefreshToken,
            DateTime expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            RotateCalls++;
            ExpectedRefreshToken = expectedRefreshToken;
            return Task.FromResult(RotateResult);
        }

        public Task RevokeRefreshTokenAsync(
            Guid userId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
