using HRM.Application.Commons.Authorization;
using HRM.Application.Features.MRO.Equipment.Dtos;
using HRM.Domain.Entities.MROSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.MRO.Equipment.Services;

internal sealed partial class EquipmentManagementService
{
    private async Task<IReadOnlyList<EquipmentSpecDto>> ReadSpecsAsync(int id, CancellationToken ct)
        => await EquipmentScope.ForSpecifications(db.EquipmentSpecsMro.AsNoTracking(), user.CompanyId, id)
            .OrderBy(x => x.SpecKey).ThenBy(x => x.SpecId)
            .Select(x => new EquipmentSpecDto(x.SpecId, x.SpecKey ?? "", x.SpecValue, x.Unit, x.Note, x.EnteredAt)).ToListAsync(ct);

    public async Task<EquipmentResult<IReadOnlyList<EquipmentSpecDto>>> GetSpecsAsync(int id, CancellationToken ct)
    {
        if (!Allowed(ApplicationPermissions.Equipment.View)) return EquipmentResult<IReadOnlyList<EquipmentSpecDto>>.Fail(403, "forbidden");
        if (!await ScopedMachines().AnyAsync(x => x.EquipmentId == id, ct))
            return EquipmentResult<IReadOnlyList<EquipmentSpecDto>>.Fail(404, "notFound");
        return new(await ReadSpecsAsync(id, ct));
    }

    public async Task<EquipmentResult<int>> SaveSpecAsync(int id, int? specId, SaveEquipmentSpecRequest request, CancellationToken ct)
    {
        var capability = specId.HasValue ? ApplicationPermissions.Equipment.Update : ApplicationPermissions.Equipment.Create;
        if (!Allowed(capability)) return EquipmentResult<int>.Fail(403, "forbidden");
        var validation = EquipmentValidation.Validate(request);
        if (validation is not null) return EquipmentResult<int>.Fail(400, validation);
        if (!await ScopedMachines().AnyAsync(x => x.EquipmentId == id, ct)) return EquipmentResult<int>.Fail(404, "notFound");
        var spec = specId.HasValue
            ? await EquipmentScope.ForSpecifications(db.EquipmentSpecsMro, user.CompanyId, id, specId).SingleOrDefaultAsync(ct)
            : new EquipmentSpecMRO { EquipmentId = id };
        if (spec is null) return EquipmentResult<int>.Fail(404, "notFound");
        spec.SpecKey = request.SpecKey.Trim();
        spec.SpecValue = Clean(request.SpecValue);
        spec.Unit = Clean(request.Unit);
        spec.Note = Clean(request.Note);
        spec.EnteredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        spec.EnteredBy = user.UserId;
        if (!specId.HasValue) db.EquipmentSpecsMro.Add(spec);
        var error = await PersistAsync(ct);
        return error is null ? new(spec.SpecId) : EquipmentResult<int>.Fail(409, error);
    }

    public async Task<EquipmentResult<bool>> DeleteSpecAsync(int id, int specId, CancellationToken ct)
    {
        if (!Allowed(ApplicationPermissions.Equipment.Delete)) return EquipmentResult<bool>.Fail(403, "forbidden");
        var spec = await EquipmentScope.ForSpecifications(db.EquipmentSpecsMro, user.CompanyId, id, specId).SingleOrDefaultAsync(ct);
        if (spec is null) return EquipmentResult<bool>.Fail(404, "notFound");
        db.EquipmentSpecsMro.Remove(spec);
        var error = await PersistAsync(ct);
        return error is null ? new(true) : EquipmentResult<bool>.Fail(409, error);
    }
}
