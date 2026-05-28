using HRM.Application.Features.Auth.Contracts;

namespace HRM.Application.Abstractions.Authentication;

public interface ITokenService
{
    AccessTokenDto CreateAccessToken(AuthenticatedUserDto user);
    string CreateRefreshToken();
}
