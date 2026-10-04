using HRM.Application.Features.MRO.Equipment.Dtos;
using HRM.Application.Features.MRO.Equipment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.MRO;

[ApiController, Authorize, Route("api/v1/mro/equipment")]
public sealed class EquipmentController(IEquipmentManagementService service) : ControllerBase
{
    [HttpGet("capabilities")]
    public IActionResult Capabilities() => Ok(service.GetCapabilities());

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] EquipmentListRequest request, CancellationToken ct)
        => Respond(await service.GetListAsync(request, ct));

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) => Respond(await service.GetOptionsAsync(ct));

    [HttpGet("{id:int:min(1)}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct) => Respond(await service.GetDetailAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveEquipmentRequest request, CancellationToken ct)
    {
        var result = await service.SaveAsync(null, request, ct);
        return result.Success ? CreatedAtAction(nameof(Detail), new { id = result.Data }, new { equipmentId = result.Data }) : Respond(result);
    }

    [HttpPut("{id:int:min(1)}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveEquipmentRequest request, CancellationToken ct)
        => Respond(await service.SaveAsync(id, request, ct));

    [HttpDelete("{id:int:min(1)}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, ct);
        return result.Success ? NoContent() : Respond(result);
    }

    [HttpGet("{id:int:min(1)}/specifications")]
    public async Task<IActionResult> Specs(int id, CancellationToken ct) => Respond(await service.GetSpecsAsync(id, ct));

    [HttpPost("{id:int:min(1)}/specifications")]
    public async Task<IActionResult> CreateSpec(int id, [FromBody] SaveEquipmentSpecRequest request, CancellationToken ct)
    {
        var result = await service.SaveSpecAsync(id, null, request, ct);
        return result.Success ? CreatedAtAction(nameof(Specs), new { id }, new { specId = result.Data }) : Respond(result);
    }

    [HttpPut("{id:int:min(1)}/specifications/{specId:int:min(1)}")]
    public async Task<IActionResult> UpdateSpec(int id, int specId, [FromBody] SaveEquipmentSpecRequest request, CancellationToken ct)
        => Respond(await service.SaveSpecAsync(id, specId, request, ct));

    [HttpDelete("{id:int:min(1)}/specifications/{specId:int:min(1)}")]
    public async Task<IActionResult> DeleteSpec(int id, int specId, CancellationToken ct)
    {
        var result = await service.DeleteSpecAsync(id, specId, ct);
        return result.Success ? NoContent() : Respond(result);
    }

    private IActionResult Respond<T>(EquipmentResult<T> result)
        => result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { code = result.ErrorCode });
}
