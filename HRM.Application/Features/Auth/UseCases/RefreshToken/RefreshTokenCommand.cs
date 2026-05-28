using HRM.Application.Abstractions.Authentication;
using HRM.Application.Features.Auth.Contracts;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Auth.UseCases.RefreshToken
{
    public sealed class RefreshTokenCommand : IRequest<LoginResultDto?>
    {
        public string RefreshToken { get; init; } = string.Empty;
    }


    internal sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResultDto?>
    {
        private readonly IIdentityAuthenticationService _identityAuthenticationService;
        private readonly ITokenService _tokenService;

        public RefreshTokenCommandHandler(
            IIdentityAuthenticationService identityAuthenticationService,
            ITokenService tokenService)
        {
            _identityAuthenticationService = identityAuthenticationService;
            _tokenService = tokenService;
        }

        public async Task<LoginResultDto?> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return null;
            }

            var user = await _identityAuthenticationService.ValidateRefreshTokenAsync(
                request.RefreshToken,
                cancellationToken);

            if (user is null)
            {
                return null;
            }

            var accessToken = _tokenService.CreateAccessToken(user);
            var newRefreshToken = _tokenService.CreateRefreshToken();
            var refreshTokenExpiresAtUtc = DateTime.Now.AddDays(7);

            await _identityAuthenticationService.StoreRefreshTokenAsync(
                user.UserId,
                newRefreshToken,
                refreshTokenExpiresAtUtc,
                cancellationToken);

            return new LoginResultDto
            {
                AccessToken = accessToken.Token,
                ExpiresAtUtc = accessToken.ExpiresAtUtc,
                RefreshToken = newRefreshToken,
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
}
