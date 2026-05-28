using System.Security.Claims;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Auth.Contracts;
using HRM.Application.Features.Auth.UseCases.Login;
using HRM.Application.Features.Auth.UseCases.Logout;
using HRM.Application.Features.Auth.UseCases.RefreshToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    private const string AccessTokenCookieName = "hrm_access_token";

    public AuthController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginHttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(
                new LoginCommand(request.UserNameOrEmail, request.Password),
                cancellationToken);

            if (result is null)
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            if (request.UseCookie)
            {
                Response.Cookies.Append(
                    AccessTokenCookieName,
                    result.AccessToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = result.ExpiresAtUtc
                    });
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RefreshTokenCommand { RefreshToken = request.RefreshToken }, cancellationToken);

        if (result is null)
        {
            return Unauthorized(new { message = "Invalid refresh token." });
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _sender.Send(new LogoutCommand(), cancellationToken);

        Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });

        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = _currentUser.UserId,
            userName = _currentUser.UserName,
            email = _currentUser.Email,
            employeeId = _currentUser.EmployeeId,
            companyId = _currentUser.CompanyId,
            roles = _currentUser.Roles
        });
    }

    public sealed class LoginHttpRequest
    {
        public string UserNameOrEmail { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;

        public bool UseCookie { get; init; }
    }
}
