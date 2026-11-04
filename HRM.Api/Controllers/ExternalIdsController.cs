using System.ComponentModel.DataAnnotations;
using HRM.Application.Abstractions.Commons.ExternalIds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/external-ids")]
public sealed class ExternalIdsController(IExternalIdService externalIds) : ControllerBase
{
    /// <summary>
    /// Cấp mã theo tháng và tăng counter dùng chung; không phải API xem trước mã.
    /// </summary>
    [HttpGet("generate")]
    [HttpGet("~/api/category/ExternalIDGenerate")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate(
        [FromQuery, Required, StringLength(50), RegularExpression(@"[A-Za-z][A-Za-z0-9_.-]*")]
        string prefix,
        CancellationToken cancellationToken)
    {
        var code = await externalIds.GenerateMonthlyCodeAsync(Guid.Empty, prefix, cancellationToken);
        return Ok(code);
    }
}
