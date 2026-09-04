using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms;

public sealed record CreateBomCommand(CreateBomRequest Request) : IRequest<OperationResult<BomVersionDto>>;
public sealed record ReplaceBomVersionCommand(Guid BomVersionId, ReplaceBomVersionRequest Request) : IRequest<OperationResult<BomVersionDto>>;
public sealed record PatchBomVersionCommand(Guid BomVersionId, PatchBomVersionRequest Request) : IRequest<OperationResult<BomVersionDto>>;

internal sealed class CreateBomCommandHandler : IRequestHandler<CreateBomCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _user;
    public CreateBomCommandHandler(IPLMWriteDbContext db, ICurrentUser user) => (_db, _user) = (db, user);

    public async Task<OperationResult<BomVersionDto>> Handle(CreateBomCommand command, CancellationToken ct)
    {
        if (!TryActor(out var companyId, out var employeeId, out var error)) return OperationResult<BomVersionDto>.Fail(error!);
        var r = command.Request;
        error = BomRules.Normalize(r.Code, 64, nameof(r.Code), true) ?? BomRules.Normalize(r.Name, 200, nameof(r.Name), true)
            ?? BomRules.Normalize(r.OutputUnit, 32, nameof(r.OutputUnit), true) ?? BomRules.ValidatePeriod(r.EffectiveFrom, r.EffectiveTo)
            ?? BomRules.ValidateItems(r.Items);
        if (error is not null || r.BaseOutputQuantity <= 0) return OperationResult<BomVersionDto>.Fail(error ?? "BaseOutputQuantity must be greater than zero.");
        if (!await _db.Products.AnyAsync(x => x.ProductId == r.ProductId && x.CompanyId == companyId && x.IsActive, ct))
            return OperationResult<BomVersionDto>.Fail("Product was not found or is outside your company.");
        if (await _db.BomDefinitions.AnyAsync(x => x.CompanyId == companyId && x.Code == r.Code.Trim(), ct))
            return OperationResult<BomVersionDto>.Fail("BOM code already exists in your company.");
        var resolved = await ResolveItemsAsync(_db, r.ProductId, r.Items, companyId, ct);
        if (resolved.Error is not null) return OperationResult<BomVersionDto>.Fail(resolved.Error);
        var now = DateTime.UtcNow;
        var definition = new BomDefinition { BomDefinitionId = Guid.CreateVersion7(), CompanyId = companyId, ProductId = r.ProductId, Code = r.Code.Trim(), Name = r.Name.Trim(), BomType = BomType.Engineering, Description = NormalizeOptional(r.Description), IsActive = true, CreatedDate = now, CreatedBy = employeeId };
        var version = new BomVersion { BomVersionId = Guid.CreateVersion7(), BomDefinitionId = definition.BomDefinitionId, VersionNo = 1, Status = BomVersionStatus.Draft, BaseOutputQuantity = r.BaseOutputQuantity, OutputUnit = r.OutputUnit.Trim(), EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, Note = NormalizeOptional(r.Note), CreatedDate = now, CreatedBy = employeeId };
        await _db.BomDefinitions.AddAsync(definition, ct);
        await _db.BomVersions.AddAsync(version, ct);
        await _db.BomVersionItems.AddRangeAsync(CreateItems(version.BomVersionId, resolved.Items!), ct);
        await _db.SaveChangesAsync(ct);
        return OperationResult<BomVersionDto>.Ok(ToDto(definition, version, resolved.Items!));
    }

    private bool TryActor(out Guid companyId, out Guid employeeId, out string? error)
    {
        companyId = _user.CompanyId ?? Guid.Empty; employeeId = _user.EmployeeId ?? Guid.Empty;
        error = companyId == Guid.Empty ? "Current company is invalid." : employeeId == Guid.Empty ? "Current user does not have an employee profile." : null;
        return error is null;
    }

    internal static async Task<ResolvedItems> ResolveItemsAsync(IPLMWriteDbContext db, Guid parentProductId, IReadOnlyList<BomItemWriteDto> items, Guid companyId, CancellationToken ct)
    {
        var materialIds = items.Where(x => x.ItemType == ItemType.Material).Select(x => x.ItemId).Distinct().ToArray();
        var productIds = items.Where(x => x.ItemType == ItemType.Product).Select(x => x.ItemId).Distinct().ToArray();
        if (productIds.Contains(parentProductId)) return new("A BOM cannot contain its own Product as a component.");
        var materials = await db.Materials.AsNoTracking().Where(x => materialIds.Contains(x.MaterialId) && x.CompanyId == companyId && x.IsActive == true).Select(x => new SourceItem(x.MaterialId, x.CategoryId, x.ExternalId, x.Name)).ToDictionaryAsync(x => x.Id, ct);
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.ProductId) && x.CompanyId == companyId && x.IsActive).Select(x => new SourceItem(x.ProductId, x.CategoryId, x.Code, x.Name)).ToDictionaryAsync(x => x.Id, ct);
        if (materials.Count != materialIds.Length || products.Count != productIds.Length) return new("One or more BOM items are inactive or outside your company.");
        var rows = new List<ResolvedItem>();
        foreach (var item in items)
        {
            var source = item.ItemType == ItemType.Material ? materials[item.ItemId] : products[item.ItemId];
            rows.Add(new(item.ItemType, item.ItemId, source.CategoryId, item.Quantity, item.Unit.Trim(), source.Code, source.Name, NormalizeOptional(item.Note)));
        }
        return new(rows);
    }

    internal static List<BomVersionItem> CreateItems(Guid versionId, IReadOnlyList<ResolvedItem> items) => items.Select((x, i) => new BomVersionItem { BomVersionItemId = Guid.CreateVersion7(), BomVersionId = versionId, LineNo = i + 1, ItemType = x.ItemType, MaterialId = x.ItemType == ItemType.Material ? x.ItemId : null, ComponentProductId = x.ItemType == ItemType.Product ? x.ItemId : null, CategoryId = x.CategoryId, Quantity = x.Quantity, Unit = x.Unit, MaterialExternalIdSnapshot = x.Code, MaterialNameSnapshot = x.Name, Note = x.Note }).ToList();
    internal static BomVersionDto ToDto(BomDefinition d, BomVersion v, IReadOnlyList<ResolvedItem> items) => new() { BomDefinitionId = d.BomDefinitionId, BomVersionId = v.BomVersionId, ProductId = d.ProductId, Code = d.Code, Name = d.Name, BomType = d.BomType, VersionNo = v.VersionNo, Status = v.Status, BaseOutputQuantity = v.BaseOutputQuantity, OutputUnit = v.OutputUnit, EffectiveFrom = v.EffectiveFrom, EffectiveTo = v.EffectiveTo, ChangeReason = v.ChangeReason, Note = v.Note, Items = items.Select((x, i) => new BomItemDto { LineNo = i + 1, ItemType = x.ItemType, ItemId = x.ItemId, CategoryId = x.CategoryId, Quantity = x.Quantity, Unit = x.Unit, ItemCode = x.Code, ItemName = x.Name, Note = x.Note }).ToList() };
    internal static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    internal sealed record SourceItem(Guid Id, Guid CategoryId, string? Code, string? Name);
    internal sealed record ResolvedItem(ItemType ItemType, Guid ItemId, Guid CategoryId, decimal Quantity, string Unit, string? Code, string? Name, string? Note);
    internal sealed class ResolvedItems { public ResolvedItems(string error) => Error = error; public ResolvedItems(IReadOnlyList<ResolvedItem> items) => Items = items; public string? Error { get; } public IReadOnlyList<ResolvedItem>? Items { get; } }
}

internal sealed class ReplaceBomVersionCommandHandler : IRequestHandler<ReplaceBomVersionCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _user;
    public ReplaceBomVersionCommandHandler(IPLMWriteDbContext db, ICurrentUser user) => (_db, _user) = (db, user);
    public async Task<OperationResult<BomVersionDto>> Handle(ReplaceBomVersionCommand command, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? Guid.Empty; var employeeId = _user.EmployeeId ?? Guid.Empty;
        if (companyId == Guid.Empty || employeeId == Guid.Empty) return OperationResult<BomVersionDto>.Fail("Current company or employee is invalid.");
        var r = command.Request; var error = BomRules.Normalize(r.OutputUnit, 32, nameof(r.OutputUnit), true) ?? BomRules.ValidatePeriod(r.EffectiveFrom, r.EffectiveTo) ?? BomRules.ValidateItems(r.Items);
        if (error is not null || r.BaseOutputQuantity <= 0) return OperationResult<BomVersionDto>.Fail(error ?? "BaseOutputQuantity must be greater than zero.");
        var version = await _db.BomVersions.Include(x => x.BomDefinition).Include(x => x.Items).FirstOrDefaultAsync(x => x.BomVersionId == command.BomVersionId && x.BomDefinition.CompanyId == companyId && x.BomDefinition.BomType == BomType.Engineering, ct);
        if (version is null) return OperationResult<BomVersionDto>.Fail("BOM version was not found.");
        if (version.Status != BomVersionStatus.Draft) return OperationResult<BomVersionDto>.Fail("Only Draft BOM versions can be changed.");
        var resolved = await CreateBomCommandHandler.ResolveItemsAsync(_db, version.BomDefinition.ProductId, r.Items, companyId, ct);
        if (resolved.Error is not null) return OperationResult<BomVersionDto>.Fail(resolved.Error);
        _db.BomVersionItems.RemoveRange(version.Items);
        version.BaseOutputQuantity = r.BaseOutputQuantity; version.OutputUnit = r.OutputUnit.Trim(); version.EffectiveFrom = r.EffectiveFrom; version.EffectiveTo = r.EffectiveTo; version.ChangeReason = CreateBomCommandHandler.NormalizeOptional(r.ChangeReason); version.Note = CreateBomCommandHandler.NormalizeOptional(r.Note);
        await _db.BomVersionItems.AddRangeAsync(CreateBomCommandHandler.CreateItems(version.BomVersionId, resolved.Items!), ct);
        version.BomDefinition.UpdatedBy = employeeId; version.BomDefinition.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult<BomVersionDto>.Ok(CreateBomCommandHandler.ToDto(version.BomDefinition, version, resolved.Items!));
    }
}

internal sealed class PatchBomVersionCommandHandler : IRequestHandler<PatchBomVersionCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _user;
    public PatchBomVersionCommandHandler(IPLMWriteDbContext db, ICurrentUser user) => (_db, _user) = (db, user);
    public async Task<OperationResult<BomVersionDto>> Handle(PatchBomVersionCommand command, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? Guid.Empty; var employeeId = _user.EmployeeId ?? Guid.Empty;
        if (companyId == Guid.Empty || employeeId == Guid.Empty) return OperationResult<BomVersionDto>.Fail("Current company or employee is invalid.");
        var v = await _db.BomVersions.Include(x => x.BomDefinition).Include(x => x.Items).FirstOrDefaultAsync(x => x.BomVersionId == command.BomVersionId && x.BomDefinition.CompanyId == companyId && x.BomDefinition.BomType == BomType.Engineering, ct);
        if (v is null) return OperationResult<BomVersionDto>.Fail("BOM version was not found."); if (v.Status != BomVersionStatus.Draft) return OperationResult<BomVersionDto>.Fail("Only Draft BOM versions can be changed.");
        var r = command.Request; var clears = r.ClearFields.Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (clears.Except(["effectiveFrom", "effectiveTo", "changeReason", "note"]).Any()) return OperationResult<BomVersionDto>.Fail("ClearFields contains an unsupported field.");
        if (r.BaseOutputQuantity is { } qty) { if (qty <= 0) return OperationResult<BomVersionDto>.Fail("BaseOutputQuantity must be greater than zero."); v.BaseOutputQuantity = qty; }
        if (r.OutputUnit is not null) { var e = BomRules.Normalize(r.OutputUnit, 32, nameof(r.OutputUnit), true); if (e is not null) return OperationResult<BomVersionDto>.Fail(e); v.OutputUnit = r.OutputUnit.Trim(); }
        if (r.EffectiveFrom is not null) v.EffectiveFrom = r.EffectiveFrom; if (r.EffectiveTo is not null) v.EffectiveTo = r.EffectiveTo;
        if (clears.Contains("effectiveFrom")) v.EffectiveFrom = null; if (clears.Contains("effectiveTo")) v.EffectiveTo = null;
        if (BomRules.ValidatePeriod(v.EffectiveFrom, v.EffectiveTo) is { } periodError) return OperationResult<BomVersionDto>.Fail(periodError);
        if (r.ChangeReason is not null) v.ChangeReason = CreateBomCommandHandler.NormalizeOptional(r.ChangeReason); if (r.Note is not null) v.Note = CreateBomCommandHandler.NormalizeOptional(r.Note);
        if (clears.Contains("changeReason")) v.ChangeReason = null; if (clears.Contains("note")) v.Note = null;
        v.BomDefinition.UpdatedBy = employeeId; v.BomDefinition.UpdatedDate = DateTime.UtcNow; await _db.SaveChangesAsync(ct);
        var items = v.Items.OrderBy(x => x.LineNo).Select(x => new CreateBomCommandHandler.ResolvedItem(x.ItemType, x.MaterialId ?? x.ComponentProductId ?? Guid.Empty, x.CategoryId ?? Guid.Empty, x.Quantity, x.Unit, x.MaterialExternalIdSnapshot, x.MaterialNameSnapshot, x.Note)).ToList();
        return OperationResult<BomVersionDto>.Ok(CreateBomCommandHandler.ToDto(v.BomDefinition, v, items));
    }
}
