using HRM.Application.Features.Auth.UseCases.Login;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Domain.Entities.Controllers.Development;

/// <summary>
/// Creates a local Swagger session from Development-only User Secrets.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/development/swagger-session")]
public sealed class SwaggerDevelopmentSessionController : ControllerBase
{
    private const string AccessTokenCookieName = "hrm_access_token";
    private const string UserNameOrEmailConfigurationKey = "SwaggerDevelopmentAutoLogin:UserNameOrEmail";
    private const string PasswordConfigurationKey = "SwaggerDevelopmentAutoLogin:Password";

    private readonly ISender _sender;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public SwaggerDevelopmentSessionController(
        ISender sender,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _sender = sender;
        _configuration = configuration;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> CreateSession(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var userNameOrEmail = _configuration[UserNameOrEmailConfigurationKey];
        var password = _configuration[PasswordConfigurationKey];
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return NotFound();
        }

        var loginResult = await _sender.Send(
            new LoginCommand(userNameOrEmail, password),
            cancellationToken);
        if (loginResult is null)
        {
            return Unauthorized();
        }

        Response.Cookies.Append(
            AccessTokenCookieName,
            loginResult.AccessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = loginResult.ExpiresAtUtc
            });

        return NoContent();
    }
}
