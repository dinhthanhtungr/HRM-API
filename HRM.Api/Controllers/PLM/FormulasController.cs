using HRM.Application.Features.PLM.Formulas.Commands.PatchFormulaPricing;
using HRM.Application.Features.PLM.Formulas.Commands.CreateFormula;
using HRM.Application.Features.PLM.Formulas.Commands.DeleteFormula;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaInformation;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.Formulas.Commands.RestoreFormulaVersion;
using HRM.Application.Features.PLM.Formulas.Commands.SaveFormulaVersion;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaById;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaLookup;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaMaterials;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaRelatedAttachments;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulas;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaVersionByNumber;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaVersions;
using HRM.Application.Features.PLM.Materials.Queries.GetFormulaItemLookup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Application.Commons.Authorization.PLM;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/formulas")]
public sealed class FormulasController : ControllerBase
{
    private readonly ISender _sender;

    public FormulasController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] GetFormulasQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup(
        [FromQuery] GetFormulaLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{formulaId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewFormulaDetail)]
    public async Task<IActionResult> GetById(
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFormulaByIdQuery
        {
            FormulaId = formulaId
        }, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpGet("{formulaId:guid}/materials")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> GetMaterials(
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFormulaMaterialsQuery
        {
            FormulaId = formulaId
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Lấy lịch sử snapshot của công thức theo VersionNo giảm dần.
    /// </summary>
    [HttpGet("{formulaId:guid}/versions")]
    [Authorize(Policy = PlmPolicies.ViewFormulaDetail)]
    public async Task<IActionResult> GetVersions(
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetFormulaVersionsQuery(formulaId),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết một snapshot theo VersionNo.
    /// </summary>
    [HttpGet("{formulaId:guid}/versions/{versionNo:int}")]
    [Authorize(Policy = PlmPolicies.ViewFormulaDetail)]
    public async Task<IActionResult> GetVersionByNumber(
        Guid formulaId,
        int versionNo,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetFormulaVersionByNumberQuery(formulaId, versionNo),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Tạo snapshot nghiệp vụ ngay cả khi dữ liệu Formula không thay đổi.
    /// </summary>
    [HttpPost("{formulaId:guid}/versions")]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> SaveVersion(
        Guid formulaId,
        [FromBody] SaveFormulaVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SaveFormulaVersionCommand(formulaId, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Khôi phục header và active materials từ version cũ, sau đó tạo version mới.
    /// </summary>
    [HttpPost("{formulaId:guid}/versions/{versionNo:int}/restore")]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    [Authorize(Policy = PlmPolicies.UpdateFormulaPricing)]
    public async Task<IActionResult> RestoreVersion(
        Guid formulaId,
        int versionNo,
        [FromBody] RestoreFormulaVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RestoreFormulaVersionCommand(formulaId, versionNo, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{formulaId:guid}/related-attachments")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> GetRelatedAttachments(
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFormulaRelatedAttachmentsQuery
        {
            FormulaId = formulaId
        }, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    /// <summary>
    /// Lookup NVL và Product hợp lệ để thêm vào công thức.
    /// </summary>
    [HttpGet("item-lookup")]
    //[Authorize(Policy = PlmPolicies.UpdateFormulaPricing)]
    public async Task<IActionResult> GetItemLookup(
        [FromQuery] GetFormulaItemLookupQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> Create(
        [FromBody] UpsertFormulaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateFormulaCommand(request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPut("{formulaId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> Update(
        Guid formulaId,
        [FromBody] UpsertFormulaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateFormulaInformationCommand(formulaId, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("{formulaId:guid}/status")]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> UpdateStatus(
        Guid formulaId,
        [FromBody] UpdateFormulaStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateFormulaStatusCommand(formulaId, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpDelete("{formulaId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> Delete(
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new DeleteFormulaCommand(formulaId),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("{formulaId:guid}/pricing")]
    [Authorize(Policy = PlmPolicies.UpdateFormulaPricing)]
    public async Task<IActionResult> PatchPricing(
        Guid formulaId,
        [FromBody] PatchFormulaPricingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PatchFormulaPricingCommand(formulaId, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
