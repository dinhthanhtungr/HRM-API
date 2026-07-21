using System.Security.Claims;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Auth.Contracts;
using HRM.Application.Features.Auth.UseCases.Login;
using HRM.Application.Features.Auth.UseCases.Logout;
using HRM.Application.Features.Auth.UseCases.RefreshToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Domain.Entities.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthController> _logger;

    private const string AccessTokenCookieName = "hrm_access_token";

    public AuthController(
        ISender sender,
        ICurrentUser currentUser,
        ILogger<AuthController> logger)
    {
        _sender = sender;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginHttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Login request received. UseCookie = {UseCookie}, Origin = {Origin}",
                request.UseCookie,
                Request.Headers.Origin.ToString());

            var result = await _sender.Send(
                new LoginCommand(request.UserNameOrEmail, request.Password),
                cancellationToken);

            if (result is null)
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            if (request.UseCookie)
            {
                SetAccessTokenCookie(result.AccessToken, result.ExpiresAtUtc);

                _logger.LogInformation(
                    "Login access token cookie appended. SetCookieLength = {SetCookieLength}",
                    Response.Headers.SetCookie.ToString().Length);
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
        _logger.LogInformation(
            "Refresh-token request received. UseCookie = {UseCookie}, HasAccessCookie = {HasAccessCookie}, Origin = {Origin}",
            request.UseCookie,
            Request.Cookies.ContainsKey(AccessTokenCookieName),
            Request.Headers.Origin.ToString());

        var result = await _sender.Send(new RefreshTokenCommand { RefreshToken = request.RefreshToken }, cancellationToken);

        if (result is null)
        {
            return Unauthorized(new { message = "Invalid refresh token." });
        }

        if (request.UseCookie || Request.Cookies.ContainsKey(AccessTokenCookieName))
        {
            SetAccessTokenCookie(result.AccessToken, result.ExpiresAtUtc);

            _logger.LogInformation(
                "Refresh access token cookie appended. SetCookieLength = {SetCookieLength}",
                Response.Headers.SetCookie.ToString().Length);
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
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
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

        public bool UseCookie { get; set; }
    }

    private void SetAccessTokenCookie(string accessToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(
            AccessTokenCookieName,
            accessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = expiresAtUtc
            });
    }
}
