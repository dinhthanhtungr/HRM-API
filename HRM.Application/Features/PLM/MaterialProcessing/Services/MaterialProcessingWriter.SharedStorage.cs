using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Category;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

public sealed class MaterialProcessingWriter(IMaterialProcessingDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, ProcessingMaterialResolver resolver,
    IExternalIdService externalIds, ProcessingWarehouseService warehouse)
{
    public async Task<MaterialProcessingResult> SaveAsync(Guid? id, SaveMaterialProcessingRequest? request, CancellationToken ct,
        PatchMaterialProcessingRequest? patch = null, Action<string, double>? recordTiming = null)
    {
        if (!CanWrite(id is null)) return new("Forbidden");
        if (id is null && !permissions.HasPermission(ApplicationPermissions.PLM.ManageMaterialProcessing)
            && request?.Status is not (nameof(ManufacturingProductOrder.New) or nameof(ManufacturingProductOrder.Scheduling))) return new("Forbidden");
        var company = user.CompanyId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        var phase = Stopwatch.GetTimestamp();
        await db.LockPurchasingCatalogAsync(company, ct);
        recordTiming?.Invoke("mpf_save_lock", Stopwatch.GetElapsedTime(phase).TotalMilliseconds);
        MfgProductionOrder? order = null;
        ManufacturingFormula? formula = null;
        if (id.HasValue)
        {
            await warehouse.LockAsync(id.Value, company, ct);
            order = await SemiFinishedProcessing.Orders(db, company).Include(x => x.Product).FirstOrDefaultAsync(x => x.MfgProductionOrderId == id, ct);
            if (order is null) return new("NotFound");
            formula = await SemiFinishedProcessing.FormulaAsync(db, company, id.Value, ct);
            if (formula is null) return new("NotFound");
            if (await warehouse.FormulaEditLockAsync(order, ct) is { } formulaLock)
                return new("Conflict", Message: formulaLock);
            if (patch?.Status == nameof(ManufacturingProductOrder.Canceled))
                return await CancelAsync(order, formula, patch, ct, transaction);
            if (order.Status == nameof(ManufacturingProductOrder.Canceled)) return new("Conflict");
            var stored = SemiFinishedProcessing.Adapt(order, formula);
            if (stored.Revision != (patch?.ExpectedRevision ?? request?.ExpectedRevision)) return new("Conflict");
            if (patch is not null)
            {
                var merged = MaterialProcessingPatch.Merge(stored, patch);
                if (merged.Error is not null) return new(merged.Error);
                request = merged.Request;
            }
            if (request is not null && (request.ProcessingType != SemiFinishedProcessing.Process(order)
                || request.MaterialId != order.ProductId
                || request.StepOfProduct.HasValue && request.StepOfProduct != order.StepOfProduct))
                return new("ImmutableIdentity");
        }
        if (request is null || request.Details is null) return new("InvalidPatch");
        var input = request with { TotalQuantityRequest = request.Details.Sum(x => x?.QuantityKg ?? 0) };
        if (input.Status == nameof(ManufacturingProductOrder.Canceled)) return new("InvalidPatch");
        var error = MaterialProcessingRules.Validate(input);
        if (error is not null) return new(error);
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ManageMaterialProcessing)
            && input.Status is not (nameof(ManufacturingProductOrder.New) or nameof(ManufacturingProductOrder.Scheduling))) return new("Forbidden");
        var materials = await resolver.InputsAsync(input.Details, company, ct);
        if (materials is null) return new("InvalidMaterials");
        input = input with { Details = materials.Select((x, i) => input.Details[i] with { MaterialId = x.MaterialId }).ToList() };
        if (materials.Select((x, i) => (x.MaterialId, input.Details[i].ItemType, Lot: input.Details[i].LotNo?.Trim().ToUpperInvariant() ?? "")).Distinct().Count() != materials.Count)
            return new("DuplicateLines");
        var stamp = Stamp(order?.UpdatedDate);
        var sourceOutput = await ProcessingProductResolver.SourceAsync(db, input.MaterialId, company, ct);
        if (sourceOutput is null) return new("InvalidSource");
        if (!materials.Any(x => x.MaterialId == sourceOutput.MaterialId)) return new("SourceRequired");
        if (input.ProcessingType == "N" && materials.Any(x => x.MaterialId != sourceOutput.MaterialId)) return new("GrindingSingleMaterial");
        if (input.Details.Any(x => x.ItemType == ItemType.MaterialFailure)
            && !permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots))
            return new("Forbidden");
        var ratios = MaterialProcessingRules.Ratios(input.Details.Select(x => x.QuantityKg).ToArray());
        if (ratios.Where((x, i) => x <= 0 || decimal.Round(x * input.TotalQuantityRequest, 3) != input.Details[i].QuantityKg).Any())
            return new("QuantityPrecision");
        var customer = await db.Customers.AsNoTracking()
            .Where(x => x.CompanyId == company && x.IsActive == true
                && x.ExternalId == PLMCustomerRules.InternalCustomerExternalId)
            .Select(x => new { x.CustomerId, x.ExternalId, x.CustomerName })
            .FirstOrDefaultAsync(ct);
        if (customer is null) return new("InternalCustomerRequired");
        var output = await ProcessingProductResolver.ResolveAsync(db, input.MaterialId, input.ProcessingType, company, user.EmployeeId!.Value, stamp, ct);
        if (output.Error is not null) return new(output.Error);
        if (order is null)
        {
            order = new MfgProductionOrder { MfgProductionOrderId = Guid.CreateVersion7(),
                ExternalId = await externalIds.GenerateMonthlyCodeAsync(company, DocumentPrefix.MPF.ToString(), ct),
                CompanyId = company, IsActive = true, CreatedBy = user.EmployeeId.Value, CreatedDate = stamp,
                RequiredDate = stamp, Status = nameof(ManufacturingProductOrder.Scheduling) };
            db.MfgProductionOrders.Add(order);
        }
        else order.Status = input.Status;
        order.CustomerId = customer.CustomerId;
        order.CustomerExternalIdSnapshot = customer.ExternalId;
        order.CustomerNameSnapshot = customer.CustomerName;
        order.ProductId = output.Product!.ProductId;
        order.Product = output.Product;
        order.ProductExternalIdSnapshot = output.Product.ColourCode;
        order.ProductNameSnapshot = output.Product.Name;
        if (MaterialProcessingRules.InternalColourName(input.ProcessingType) is { } colour)
            order.ColorName = colour;
        order.TotalQuantity = input.TotalQuantityRequest;
        order.TotalQuantityRequest = input.TotalQuantityRequest;
        order.NumOfBatches = input.NumOfBatches;
        order.StepOfProduct = MaterialProcessingRules.Step(input.ProcessingType);
        order.ManufacturingDate = input.ManufacturingDate;
        order.LabNote = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        order.QcCheck = string.IsNullOrWhiteSpace(input.QcCheck) ? null : input.QcCheck.Trim();
        order.UpdatedBy = user.EmployeeId.Value;
        order.UpdatedDate = stamp;
        phase = Stopwatch.GetTimestamp();
        var resolved = await warehouse.ResolveAsync(order, formula, input, ratios, ct);
        recordTiming?.Invoke("mpf_save_warehouse_input", Stopwatch.GetElapsedTime(phase).TotalMilliseconds);
        if (resolved.Items is null) return new("InvalidWarehouseInput", Message: resolved.Error);
        if (formula is null)
        {
            formula = new ManufacturingFormula { ManufacturingFormulaId = Guid.CreateVersion7(),
                ExternalId = order.ExternalId,
                Name = "F001", CompanyId = company, IsActive = true, Status = nameof(ManufacturingProductOrderFormula.Checking),
                SourceType = FormulaSource.FromVA, CreatedBy = user.EmployeeId.Value, CreatedDate = stamp };
            db.ManufacturingFormulas.Add(formula);
            db.ProductionSelectVersions.Add(new ProductionSelectVersion { ProductionSelectVersionId = Guid.CreateVersion7(),
                MfgProductionOrderId = order.MfgProductionOrderId, MfgProductionOrder = order,
                ManufacturingFormulaId = formula.ManufacturingFormulaId, ManufacturingFormula = formula,
                CompanyId = company, CreatedBy = user.EmployeeId.Value, ValidFrom = stamp });
        }
        else
        {
            db.ManufacturingFormulaMaterials.RemoveRange(formula.ManufacturingFormulaMaterials);
            formula.ManufacturingFormulaMaterials.Clear();
        }
        formula.Note = order.LabNote;
        formula.TotalPrice = resolved.Items.Sum(x => x.UnitPrice * x.Quantity);
        formula.UpdatedBy = user.EmployeeId.Value;
        formula.UpdatedDate = stamp;
        for (var index = 0; index < materials.Count; index++)
        {
            var material = materials[index];
            var detail = new ManufacturingFormulaMaterial { ManufacturingFormulaMaterialId = Guid.CreateVersion7(),
                ManufacturingFormulaId = formula.ManufacturingFormulaId, ManufacturingFormula = formula,
                MaterialId = material.MaterialId, Material = material, CategoryId = material.CategoryId, Unit = material.Unit,
                Quantity = ratios[index], UnitPrice = resolved.Items[index].UnitPrice,
                TotalPrice = ratios[index] * resolved.Items[index].UnitPrice,
                itemType = input.Details[index].ItemType, LineNo = index + 1, IsActive = true,
                LotNo = string.IsNullOrWhiteSpace(input.Details[index].LotNo) ? null : input.Details[index].LotNo!.Trim(),
                MaterialExternalIdSnapshot = material.ExternalId, MaterialNameSnapshot = material.Name };
            formula.ManufacturingFormulaMaterials.Add(detail);
            db.ManufacturingFormulaMaterials.Add(detail);
        }
        phase = Stopwatch.GetTimestamp();
        var reservationError = await warehouse.ReserveAsync(order, resolved.Items, stamp, ct);
        recordTiming?.Invoke("mpf_save_reservation", Stopwatch.GetElapsedTime(phase).TotalMilliseconds);
        if (reservationError is not null) return new("InvalidWarehouseInput", Message: reservationError);
        var schedule = await db.SchedualMfgs.FirstOrDefaultAsync(x => x.MfgProductionOrderId == order.MfgProductionOrderId
            && x.MfgProductionOrder != null && x.MfgProductionOrder.CompanyId == company, ct);
        if (schedule is null && order.Status == nameof(ManufacturingProductOrder.Scheduling))
            db.SchedualMfgs.Add(ProductionOrderFormulaFactory.CreateSchedule(order, stamp));
        else if (schedule is not null)
        {
            schedule.DeliveryPlanDate = order.ExpectedDate;
            schedule.Note = order.LabNote ?? order.PlpuNote;
            schedule.requirement = order.Requirement;
            schedule.StepOfProduct = order.StepOfProduct;
        }
        phase = Stopwatch.GetTimestamp();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        recordTiming?.Invoke("mpf_save_commit", Stopwatch.GetElapsedTime(phase).TotalMilliseconds);
        return new("Ok", MaterialProcessingProjection.ToDto(SemiFinishedProcessing.Adapt(order, formula), output.Source));
    }

    public async Task<MaterialProcessingResult> ConfirmAsync(Guid id, ConfirmMaterialProcessingRequest request, CancellationToken ct)
    {
        if (!CanWrite()) return new("Forbidden");
        var company = user.CompanyId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(company, ct);
        var order = await SemiFinishedProcessing.Orders(db, company).Include(x => x.Product).FirstOrDefaultAsync(x => x.MfgProductionOrderId == id, ct);
        var formula = order is null ? null : await SemiFinishedProcessing.FormulaAsync(db, company, id, ct);
        if (order is null || formula is null) return new("NotFound");
        if (order.Status != nameof(ManufacturingProductOrder.FormulaSuccess))
        {
            if (SemiFinishedProcessing.Revision(order, formula) != request.ExpectedRevision || !MaterialProcessingRules.CanConfirm(order.Status)) return new("Conflict");
            order.Status = nameof(ManufacturingProductOrder.FormulaSuccess);
            order.UpdatedDate = Stamp(order.UpdatedDate);
            order.UpdatedBy = user.EmployeeId!.Value;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new("Ok", MaterialProcessingProjection.ToDto(SemiFinishedProcessing.Adapt(order, formula)));
    }

    private async Task<MaterialProcessingResult> CancelAsync(MfgProductionOrder order, ManufacturingFormula formula,
        PatchMaterialProcessingRequest patch, CancellationToken ct, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        if (patch.ExpectedRevision <= 0) return new("Conflict");
        if (patch.ProcessingType is not null || patch.MaterialId.HasValue || patch.TotalQuantityRequest.HasValue
            || patch.NumOfBatches.HasValue || patch.StepOfProduct.HasValue || patch.ManufacturingDate.HasValue || patch.Note is not null
            || patch.QcCheck is not null || patch.Details is not null || patch.ClearFields is { Count: > 0 }) return new("InvalidPatch");
        if (order.Status != nameof(ManufacturingProductOrder.Canceled))
        {
            if (patch.ExpectedRevision != SemiFinishedProcessing.Revision(order, formula)) return new("Conflict");
            order.Status = nameof(ManufacturingProductOrder.Canceled);
            order.UpdatedDate = Stamp(order.UpdatedDate);
            order.UpdatedBy = user.EmployeeId!.Value;
            await warehouse.CancelAsync(order, ct);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new("Ok", MaterialProcessingProjection.ToDto(SemiFinishedProcessing.Adapt(order, formula)));
    }

    private DateTime Stamp(DateTime? previous)
    {
        var now = clock.Now;
        var ticks = now.Ticks - now.Ticks % 10;
        return new DateTime(previous.HasValue && ticks <= previous.Value.Ticks ? previous.Value.Ticks + 10 : ticks, now.Kind);
    }
    private bool CanWrite(bool creating = false) => user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty
        && user.EmployeeId is { } employee && employee != Guid.Empty
        && (permissions.HasPermission(ApplicationPermissions.PLM.ManageMaterialProcessing)
            || creating && permissions.HasPermission(ApplicationPermissions.PLM.ViewMaterialProcessing)
                && permissions.HasPermission(ApplicationPermissions.PLM.CreateMaterialProcessing));
}
