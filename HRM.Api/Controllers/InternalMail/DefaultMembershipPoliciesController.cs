using HRM.Application.Features.InternalMail.DefaultMembership;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.InternalMail;

[ApiController]
[Authorize]
[Route("api/v1/internal-mail/default-membership")]
public sealed class DefaultMembershipPoliciesController(DefaultMembershipPolicyService policies) : ControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) => Respond(await policies.OptionsAsync(ct));

    [HttpGet("policies")]
    public async Task<IActionResult> Policies(CancellationToken ct) => Respond(await policies.PoliciesAsync(ct));

    [HttpGet("policies/{groupType}/revisions")]
    public async Task<IActionResult> Revisions(InternalConversationGroupType groupType, CancellationToken ct,
        [FromQuery] int take = 50, [FromQuery] int? beforeVersion = null) =>
        Respond(await policies.RevisionsAsync(groupType, take, beforeVersion, ct));

    [HttpPut("policies/{groupType}")]
    public async Task<IActionResult> Publish(InternalConversationGroupType groupType, [FromBody] PublishDefaultMembershipRequest request, CancellationToken ct) =>
        Respond(await policies.PublishAsync(groupType, request, ct));

    [HttpPost("policies/{groupType}/rollback")]
    public async Task<IActionResult> Rollback(InternalConversationGroupType groupType, [FromBody] RollbackDefaultMembershipRequest request, CancellationToken ct) =>
        Respond(await policies.RollbackAsync(groupType, request, ct));

    private IActionResult Respond<T>(MembershipRequestResult<T> result) => result.Status switch
    {
        MembershipRequestStatus.Success => Ok(result.Value),
        MembershipRequestStatus.Forbidden => Forbid(),
        MembershipRequestStatus.NotFound => NotFound(new { code = result.ErrorCode }),
        MembershipRequestStatus.Conflict => Conflict(new { code = result.ErrorCode }),
        _ => BadRequest(new { code = result.ErrorCode })
    };
}
