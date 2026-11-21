using HRM.Application.Features.DevAndQA.CoaCertificates.Queries.GetCoaCertificates;
using HRM.Application.Features.DevAndQA.CoaCertificates.Queries.GetCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CreateCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.AddCoaCertificateResult;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificateResult;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.IssueCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CancelCoaCertificate;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/coa-certificates")]
public sealed class CoaCertificatesController(ISender sender) : EvaluationControllerBase(sender)
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet]
    public Task<IActionResult> List([FromQuery] GetCoaCertificatesQuery query, CancellationToken ct) => Execute(query, ct);
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => Execute(new GetCoaCertificateQuery(id), ct);
    [HttpPost]
    public Task<IActionResult> Create(CreateCoaCertificateCommand request, CancellationToken ct) => Execute(request, ct, true);
    [HttpPatch("{id:guid}")]
    public Task<IActionResult> Patch(Guid id, PatchCoaCertificateCommand request, CancellationToken ct) => Execute(request with { Id = id }, ct);
    [HttpPost("{id:guid}/results")]
    public Task<IActionResult> Add(Guid id, AddCoaCertificateResultCommand request, CancellationToken ct) => Execute(request with { CertificateId = id }, ct, true);
    [HttpPatch("{id:guid}/results/{resultId:guid}")]
    public Task<IActionResult> PatchResult(Guid id, Guid resultId, PatchCoaCertificateResultCommand request, CancellationToken ct) => Execute(request with { CertificateId = id, ResultId = resultId }, ct);
    [HttpPost("{id:guid}/issue")]
    public Task<IActionResult> Issue(Guid id, CancellationToken ct) => Execute(new IssueCoaCertificateCommand(id), ct);
    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => Execute(new CancelCoaCertificateCommand(id), ct);
}
