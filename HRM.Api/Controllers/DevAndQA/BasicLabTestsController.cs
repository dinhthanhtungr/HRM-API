using HRM.Application.Features.DevAndQA.LabTests.Commands.CreateBasicLabTest;
using HRM.Application.Features.DevAndQA.LabTests.Commands.PatchBasicLabTest;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetBasicLabTest;
using HRM.Application.Features.DevAndQA.LabTests.Queries.ListBasicLabTests;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTestLookup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

/// <summary>Tạo/xem/sửa mẫu test cơ bản, chọn Material hoặc nhập tự do, độc lập voucher; không mở import.</summary>
[Authorize]
[ApiController]
[Route("api/v1/dev-and-qa/lab-tests")]
public sealed class BasicLabTestsController(ISender sender) : EvaluationControllerBase(sender)
{
    [HttpGet("lookups/materials")]
    public Task<IActionResult> Materials(CancellationToken ct, string? keyword = null, int pageNumber = 1, int pageSize = 20, Guid? selectedId = null) =>
        Execute(new GetLabTestLookupQuery(LabTestLookupKind.Material, keyword, pageNumber, pageSize, selectedId), ct);

    [HttpGet("lookups/suppliers")]
    public Task<IActionResult> Suppliers(CancellationToken ct, string? keyword = null, int pageNumber = 1, int pageSize = 20, Guid? selectedId = null) =>
        Execute(new GetLabTestLookupQuery(LabTestLookupKind.Supplier, keyword, pageNumber, pageSize, selectedId), ct);

    [HttpGet("lookups/lab-employees")]
    public Task<IActionResult> LabEmployees(CancellationToken ct, string? keyword = null, int pageNumber = 1, int pageSize = 20, Guid? selectedId = null) =>
        Execute(new GetLabTestLookupQuery(LabTestLookupKind.LabEmployee, keyword, pageNumber, pageSize, selectedId), ct);
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 15) =>
        Execute(new ListBasicLabTestsQuery(pageNumber, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => Execute(new GetBasicLabTestQuery(id), ct);

    [HttpPost]
    public Task<IActionResult> Create(BasicLabTestWriteRequest request, CancellationToken ct) => Execute(new CreateBasicLabTestCommand(request), ct, true);

    [HttpPatch("{id:guid}")]
    public Task<IActionResult> Patch(Guid id, BasicLabTestWriteRequest request, CancellationToken ct) => Execute(new PatchBasicLabTestCommand(id, request), ct);
}
