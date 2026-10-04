using HRM.Application.Commons.Authorization;
using HRM.Application.Features.MRO.Equipment.Dtos;
using HRM.Domain.Entities.MROSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.MRO.Equipment.Services;

internal sealed partial class EquipmentManagementService
{
    public async Task<EquipmentResult<int>> SaveAsync(int? id, SaveEquipmentRequest request, CancellationToken ct)
    {
        var capability = id.HasValue ? ApplicationPermissions.Equipment.Update : ApplicationPermissions.Equipment.Create;
        if (!Allowed(capability)) return EquipmentResult<int>.Fail(403, "forbidden");
        var validation = EquipmentValidation.Validate(request);
        if (validation is not null) return EquipmentResult<int>.Fail(400, validation);
        var machine = id.HasValue ? await ScopedMachines().SingleOrDefaultAsync(x => x.EquipmentId == id, ct) : new EquipmentMRO();
        if (machine is null) return EquipmentResult<int>.Fail(404, "notFound");
        var code = request.EquipmentExternalId.Trim();
        if (await ScopedMachines().AnyAsync(x => x.EquipmentExternalId == code && x.EquipmentId != (id ?? 0), ct))
            return EquipmentResult<int>.Fail(409, "duplicateCode");
        var company = await db.Companies.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && x.IsActive)
            .Select(x => new { x.Code }).SingleOrDefaultAsync(ct);
        var area = await db.AreasMro.AsNoTracking().Where(x => x.AreaId == request.AreaId)
            .Select(x => new { x.AreaExternalId }).SingleOrDefaultAsync(ct);
        var part = await db.Parts.AsNoTracking().Where(x => x.PartId == request.PartId)
            .Select(x => new { x.ExternalId }).SingleOrDefaultAsync(ct);
        if (company is null || string.IsNullOrWhiteSpace(company.Code)) return EquipmentResult<int>.Fail(400, "missingCompanyCode");
        if (area is null || part is null) return EquipmentResult<int>.Fail(400, "invalidReference");
        if (request.Details.EquipmentTypeId is { } typeId && !await db.EquipmentTypesMro.AnyAsync(x => x.EquipmentTypeId == typeId, ct))
            return EquipmentResult<int>.Fail(400, "invalidReference");
        machine.EquipmentExternalId = code;
        machine.EquipmentName = request.EquipmentName.Trim();
        machine.GroupType = Clean(request.GroupType);
        machine.FactoryId = user.CompanyId!.Value;
        machine.FactoryExternalId = company.Code;
        machine.AreaId = request.AreaId;
        machine.AreaExternalId = area.AreaExternalId;
        machine.PartId = request.PartId;
        machine.PartExternalId = part.ExternalId;
        if (!id.HasValue) db.EquipmentsMro.Add(machine);
        var details = id.HasValue
            ? await db.EquipmentDetailsMro.SingleOrDefaultAsync(x => x.EquipmentId == id && x.Equipment.FactoryId == user.CompanyId, ct)
            : null;
        if (details is null)
        {
            details = new EquipmentDetailMRO { Equipment = machine };
            db.EquipmentDetailsMro.Add(details);
        }
        details.SerialNo = Clean(request.Details.SerialNo);
        details.Manufacturer = Clean(request.Details.Manufacturer);
        details.Model = Clean(request.Details.Model);
        details.PurchaseDate = request.Details.PurchaseDate?.Date;
        details.CommissioningDate = request.Details.CommissioningDate?.Date;
        details.WarrantyUntil = request.Details.WarrantyUntil?.Date;
        details.Notes = Clean(request.Details.Notes);
        details.EquipmentTypeId = request.Details.EquipmentTypeId;
        details.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        details.UpdatedBy = user.UserId;
        var error = await PersistAsync(ct);
        return error is null ? new(machine.EquipmentId) : EquipmentResult<int>.Fail(409, error);
    }

    public async Task<EquipmentResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        if (!Allowed(ApplicationPermissions.Equipment.Delete)) return EquipmentResult<bool>.Fail(403, "forbidden");
        var machine = await ScopedMachines().SingleOrDefaultAsync(x => x.EquipmentId == id, ct);
        if (machine is null) return EquipmentResult<bool>.Fail(404, "notFound");
        // Existing FK constraints prevent deleting machines used by BOMs or other records.
        var deleted = false;
        var error = await PersistAsync(ct, async () => { deleted = await db.DeleteEquipmentAsync(machine, ct); });
        return error is not null ? EquipmentResult<bool>.Fail(409, error)
            : deleted ? new(true) : EquipmentResult<bool>.Fail(409, "hasAssessment");
    }
}
