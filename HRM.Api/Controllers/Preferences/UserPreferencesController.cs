using HRM.Application.Features.UserPreferences.Commands.SaveUserPreference;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Application.Features.UserPreferences.Queries.GetUserPreferences;
using HRM.Application.Features.UserPreferences.Queries.GetUserPreference;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Preferences;

[ApiController]
[Authorize]
[Route("api/v1/me/preferences")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserPreferencesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMany([FromQuery] string? categories,
        CancellationToken cancellationToken)
    {
        var names = (categories ?? "").Split(',', StringSplitOptions.TrimEntries);
        var result = await sender.Send(new GetUserPreferencesQuery(names), cancellationToken);
        return Respond(result);
    }

    [HttpGet("{category}")]
    public async Task<IActionResult> Get(string category, CancellationToken cancellationToken)
    {
        return Respond(await sender.Send(new GetUserPreferenceQuery(category), cancellationToken));
    }

    [HttpPut("{category}")]
    [RequestSizeLimit(20 * 1024)]
    public async Task<IActionResult> Save(string category, [FromBody] SaveUserPreferenceRequest request,
        CancellationToken cancellationToken) =>
        Respond(await sender.Send(new SaveUserPreferenceCommand(category, request), cancellationToken));

    private IActionResult Respond<T>(PreferenceResult<T> result) => result.Error switch
    {
        PreferenceError.None => Ok(result.Data),
        PreferenceError.Unauthorized => Unauthorized(),
        PreferenceError.Invalid => BadRequest(new { code = "invalid_preference" }),
        PreferenceError.Conflict => Conflict(new { code = "preference_conflict" }),
        PreferenceError.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { code = "preferences_unavailable" }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
