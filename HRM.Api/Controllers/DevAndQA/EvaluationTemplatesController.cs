using HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetEvaluationTemplates;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetDefaultEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.CreateEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PatchEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PublishEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.RetireEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.SyncTemplateFromCatalog;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.PreviewTemplateCatalogSync;
using HRM.Domain.Enums.Devandqa;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/evaluation-templates")]
public sealed class EvaluationTemplatesController(ISender sender) : EvaluationControllerBase(sender)
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] GetEvaluationTemplatesQuery query, CancellationToken ct) => Execute(query, ct);
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => Execute(new GetEvaluationTemplateQuery(id), ct);
    [HttpGet("default/{type}")]
    public Task<IActionResult> Default(EvaluationTemplateType type, CancellationToken ct) => Execute(new GetDefaultEvaluationTemplateQuery(type), ct);
    [HttpPost]
    public Task<IActionResult> Create(CreateEvaluationTemplateCommand request, CancellationToken ct) => Execute(request, ct, true);
    [HttpPatch("{id:guid}")]
    public Task<IActionResult> Patch(Guid id, PatchEvaluationTemplateCommand request, CancellationToken ct) => Execute(request with { Id = id }, ct);
    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, PublishEvaluationTemplateCommand request, CancellationToken ct) => Execute(request with { Id = id }, ct);
    /// <summary>Thay dữ liệu chỉ tiêu từ danh mục theo một hoặc nhiều mã; không thêm/bỏ dòng mẫu.</summary>
    [HttpPost("{id:guid}/sync-from-catalog")]
    public Task<IActionResult> SyncFromCatalog(Guid id, SyncTemplateFromCatalogCommand request, CancellationToken ct) =>
        Execute(request with { Id = id }, ct);
    /// <summary>Xem trước thay đổi trước khi xác nhận đồng bộ; không ghi database.</summary>
    [HttpPost("{id:guid}/catalog-sync-preview")]
    public Task<IActionResult> PreviewCatalogSync(Guid id, PreviewTemplateCatalogSyncQuery request, CancellationToken ct) =>
        Execute(request with { Id = id }, ct);
    [HttpPost("{id:guid}/retire")]
    public Task<IActionResult> Retire(Guid id, CancellationToken ct) => Execute(new RetireEvaluationTemplateCommand(id), ct);
}
