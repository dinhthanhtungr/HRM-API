using HRM.Application.Abstractions.Authentication;
using HRM.Application.Features.Auth.Contracts;
using MediatR;

namespace HRM.Application.Features.Auth.UseCases.Login;

public sealed class LoginCommand : IRequest<LoginResultDto?>
{
    public LoginCommand(string userNameOrEmail, string password)
    {
        UserNameOrEmail = userNameOrEmail;
        Password = password;
    }

    public string UserNameOrEmail { get; }

    public string Password { get; }
}

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResultDto?>
{
    private readonly IIdentityAuthenticationService _identityAuthenticationService;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        IIdentityAuthenticationService identityAuthenticationService,
        ITokenService tokenService)
    {
        _identityAuthenticationService = identityAuthenticationService;
        _tokenService = tokenService;
    }

    public async Task<LoginResultDto?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Username and password are required.");
        }

        var user = await _identityAuthenticationService.ValidateUserAsync(
            request.UserNameOrEmail,
            request.Password,
            cancellationToken);


        if (user is null)
        {
            return null;
        }

        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = _tokenService.CreateRefreshToken();
        var refreshTokenExpiresAtUtc = DateTime.Now.AddDays(7);

        await _identityAuthenticationService.StoreRefreshTokenAsync(
            user.UserId,
            refreshToken,
            refreshTokenExpiresAtUtc,
            cancellationToken);


        return new LoginResultDto
        {
            AccessToken = accessToken.Token,
            ExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpireAtUtc = refreshTokenExpiresAtUtc,
            UserId = user.UserId,
            UserName = user.UserName,
            Email = user.Email,
            EmployeeId = user.EmployeeId,
            CompanyId = user.CompanyId,
            Roles = user.Roles
        };

    }
}
