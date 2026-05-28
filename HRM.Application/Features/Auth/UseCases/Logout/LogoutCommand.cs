using HRM.Application.Abstractions.Authentication;
using HRM.Application.Abstractions.Security;
using MediatR;

namespace HRM.Application.Features.Auth.UseCases.Logout;

public sealed record LogoutCommand : IRequest<Unit>;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IIdentityAuthenticationService _identityAuthenticationService;
    private readonly ICurrentUser _currentUser;

    public LogoutCommandHandler(
        IIdentityAuthenticationService identityAuthenticationService,
        ICurrentUser currentUser)
    {
        _identityAuthenticationService = identityAuthenticationService;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _identityAuthenticationService.RevokeRefreshTokenAsync(_currentUser.UserId, cancellationToken);
        return Unit.Value;
    }
}
