using System.Globalization;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords;
using HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingColorChips;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.ColorChips;

internal sealed class ManufacturingColorChipService(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IColorChipRecordPdfRenderer renderer
)
{
    private string? Access(bool write) => ManufacturingAccess.Validate(
        user,
        permissions,
        write ? ApplicationPermissions.PLM.ManageManufacturingColorChips : ApplicationPermissions.PLM.ViewProductionOrders,
        write
    );
    private Guid Company => user.CompanyId!.Value;

    private IQueryable<ColorChipManufacturingRecord> Scoped(bool tracked = false)
    {
        var query = db.ColorChipManufacturingRecords.Where(x => x.CompanyId == Company
            && (x.MfgProductionOrder == null || x.MfgProductionOrder.CompanyId == Company)
            && (x.ManufacturingFormula == null || x.ManufacturingFormula.CompanyId == Company));
        return tracked ? query : query.AsNoTracking();
    }

    public async Task<OperationResult<GetColorChipManufacturingRecord>> SaveAsync(
        Guid? id,
        ManufacturingColorChipInput request,
        CancellationToken ct
    )
    {
        var error = Access(true);
        if (error is not null)
        {
            return Fail(error);
        }

        if (id == Guid.Empty || request.MfgProductionOrderId == Guid.Empty
            || request.ManufacturingFormulaId == Guid.Empty
            || request.ResinType.HasValue && !Enum.IsDefined(request.ResinType.Value)
            || request.LogoType.HasValue && !Enum.IsDefined(request.LogoType.Value)
            || request.FormStyle.HasValue && !Enum.IsDefined(request.FormStyle.Value)
            || request.UsageRate.HasValue && !double.IsFinite(request.UsageRate.Value))
        {
            return Fail("Dữ liệu color-chip không hợp lệ.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            var recordId = id ?? Guid.CreateVersion7();
            await db.LockManufacturingColorChipAsync(recordId, Company, ct);
            var entity = id.HasValue ? await Scoped(true).FirstOrDefaultAsync(x => x.ColorChipMfgRecordId == id && x.IsActive, ct) : null;
            if (id.HasValue && entity is null)
            {
                return Fail("Không tìm thấy color-chip active trong công ty.");
            }

            MfgProductionOrder? order = null;
            ManufacturingFormula? formula = null;
            if (request.MfgProductionOrderId.HasValue)
            {
                await db.LockProductionOrderAsync(request.MfgProductionOrderId.Value, Company, ct);
                order = await db.MfgProductionOrders.FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.MfgProductionOrderId && x.CompanyId == Company && x.IsActive, ct);
                if (order is null)
                {
                    return Fail("Lệnh sản xuất không active trong công ty.");
                }
            }

            var formulaId = request.ManufacturingFormulaId;
            if (!id.HasValue && order is not null && formulaId is null)
            {
                formulaId = await db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == Company && x.MfgProductionOrderId == order.MfgProductionOrderId && x.ValidTo == null)
                    .OrderByDescending(x => x.ValidFrom)
                    .ThenByDescending(x => x.ProductionSelectVersionId)
                    .Select(x => (Guid? )x.ManufacturingFormulaId)
                    .FirstOrDefaultAsync(ct);
            }

            if (formulaId.HasValue)
            {
                if (order is not null
                    && !await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == Company && x.MfgProductionOrderId == order.MfgProductionOrderId
                    && x.ManufacturingFormulaId == formulaId, ct))
                {
                    return Fail("VA không thuộc lệnh sản xuất.");
                }

                await db.LockManufacturingFormulaAsync(formulaId.Value, Company, ct);
                formula = await db.ManufacturingFormulas.FirstOrDefaultAsync(x => x.ManufacturingFormulaId == formulaId && x.CompanyId == Company && x.IsActive, ct);
                if (formula is null)
                {
                    return Fail("VA không active trong công ty.");
                }
            }

            var now = clock.Now;
            entity ??= new ColorChipManufacturingRecord

            {

                ColorChipMfgRecordId = recordId,

                CompanyId = Company,

                IsActive = true,

                CreatedBy = user.EmployeeId!.Value,

                CreatedDate = now,

                ResinType = ResinType.PP,

                LogoType = LogoType.Vietaus,

                FormStyle = FormStyle.Chips2

            };
            var changed = Apply(entity, request, order, formula, !id.HasValue, now);
            if (!id.HasValue)
            {
                await db.ColorChipManufacturingRecords.AddAsync(entity, ct);
            }
            else if (changed)
            {
                entity.UpdatedBy = user.EmployeeId!.Value;
                entity.UpdatedDate = now;
            }

            if (!id.HasValue || changed)
            {
                await db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return OperationResult<GetColorChipManufacturingRecord>.Ok(Map(new[]
            {
                entity
            }.AsQueryable()).Single(), changed ? "Đã lưu color-chip sản xuất." : "Không có dữ liệu thay đổi.");
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return Fail("Không thể lưu color-chip sản xuất.");
        }
    }

    private static bool Apply(
        ColorChipManufacturingRecord entity,
        ManufacturingColorChipInput r,
        MfgProductionOrder? order,
        ManufacturingFormula? formula,
        bool create,
        DateTime now
    )
    {
        var changed = PatchHelper.SetIfHasValue(r.ResinType, () => entity.ResinType, v => entity.ResinType = v);
        changed |= PatchHelper.SetIfHasValue(r.LogoType, () => entity.LogoType, v => entity.LogoType = v);
        changed |= PatchHelper.SetIfHasValue(r.FormStyle, () => entity.FormStyle, v => entity.FormStyle = v);
        changed |= PatchHelper.SetNullableRef(
            create ? r.ColorCode ?? order?.ProductExternalIdSnapshot : r.ColorCode,
            () => entity.ColorCode,
            v => entity.ColorCode = v
        );
        changed |= PatchHelper.SetNullableRef(
            create ? r.ProductName ?? order?.ProductNameSnapshot : r.ProductName,
            () => entity.ProductName,
            v => entity.ProductName = v
        );
        changed |= PatchHelper.SetNullable(r.MfgProductionOrderId, () => entity.MfgProductionOrderId, v => entity.MfgProductionOrderId = v);
        changed |= PatchHelper.SetNullable(
            formula?.ManufacturingFormulaId,
            () => entity.ManufacturingFormulaId,
            v => entity.ManufacturingFormulaId = v
        );
        changed |= PatchHelper.SetNullableRef(r.StandardFormula, () => entity.StandardFormula, v => entity.StandardFormula = v);
        changed |= PatchHelper.SetNullableRef(r.Machine, () => entity.Machine, v => entity.Machine = v);
        changed |= PatchHelper.SetNullableRef(r.Resin, () => entity.Resin, v => entity.Resin = v);
        changed |= PatchHelper.SetNullableRef(
            create ? r.TemperatureLimit ?? string.Empty : r.TemperatureLimit,
            () => entity.TemperatureLimit,
            v => entity.TemperatureLimit = v
        );
        changed |= PatchHelper.SetNullableRef(r.SizeText, () => entity.SizeText, v => entity.SizeText = v);
        changed |= PatchHelper.SetNullable(r.PelletWeightGram, () => entity.PelletWeightGram, v => entity.PelletWeightGram = v);
        changed |= PatchHelper.SetNullableRef(r.NetWeightGram, () => entity.NetWeightGram, v => entity.NetWeightGram = v);
        changed |= PatchHelper.SetNullable(r.Electrostatic, () => entity.Electrostatic, v => entity.Electrostatic = v);
        changed |= PatchHelper.SetNullableRef(r.DeltaE, () => entity.DeltaE, v => entity.DeltaE = v);
        changed |= PatchHelper.SetNullable(r.UsageRate, () => entity.UsageRate, v => entity.UsageRate = v);
        changed |= PatchHelper.SetNullable(create ? r.RecordDate ?? now : r.RecordDate, () => entity.RecordDate, v => entity.RecordDate = v);
        changed |= PatchHelper.SetNullableRef(r.Note, () => entity.Note, v => entity.Note = v);
        changed |= PatchHelper.SetNullableRef(r.PrintNote, () => entity.PrintNote, v => entity.PrintNote = v);
        entity.MfgProductionOrder = order;
        entity.ManufacturingFormula = formula;
        return changed;
    }

    public async Task<OperationResult<GetColorChipManufacturingRecord>> GetAsync(Guid id, CancellationToken ct)
    {
        var error = Access(false);
        if (error is not null)
        {
            return Fail(error);
        }

        var data = await Map(Scoped().Where(x => x.ColorChipMfgRecordId == id && x.IsActive)).FirstOrDefaultAsync(ct);
        return data is null ? Fail("Không tìm thấy color-chip sản xuất trong công ty.") : OperationResult<GetColorChipManufacturingRecord>.Ok(data);
    }

    public async Task<OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>> ListAsync(ListManufacturingColorChipsQuery r, CancellationToken ct)
    {
        var error = Access(false);
        if (error is not null)
        {
            return OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>.Fail(error);
        }

        var skip = (long)(r.PageNumber - 1) * r.PageSize;
        if (r.PageNumber <= 0 || r.PageSize is < 1 or > 100 || skip > int.MaxValue || r.From > r.To
            || r.ResinType.HasValue && !Enum.IsDefined(r.ResinType.Value)
            || r.LogoType.HasValue && !Enum.IsDefined(r.LogoType.Value)
            || r.FormStyle.HasValue && !Enum.IsDefined(r.FormStyle.Value))
        {
            return OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>.Fail("Bộ lọc không hợp lệ.");
        }

        if (r.IsActive == false
            && !permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingColorChips))
        {
            return OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>.Fail("Không có quyền xem color-chip inactive.");
        }

        var query = Scoped().Where(x => x.IsActive == r.IsActive);
        if (r.Id.HasValue)
        {
            query = query.Where(x => x.ColorChipMfgRecordId == r.Id);
        }

        if (r.OrderId.HasValue)
        {
            query = query.Where(x => x.MfgProductionOrderId == r.OrderId);
        }

        if (r.FormulaId.HasValue)
        {
            query = query.Where(x => x.ManufacturingFormulaId == r.FormulaId);
        }

        if (r.ResinType.HasValue)
        {
            query = query.Where(x => x.ResinType == r.ResinType);
        }

        if (r.LogoType.HasValue)
        {
            query = query.Where(x => x.LogoType == r.LogoType);
        }

        if (r.FormStyle.HasValue)
        {
            query = query.Where(x => x.FormStyle == r.FormStyle);
        }

        if (r.From.HasValue)
        {
            query = query.Where(x => x.RecordDate >= r.From);
        }

        if (r.To.HasValue)
        {
            query = query.Where(x => x.RecordDate <= r.To);
        }

        var smartSearch = HRM.Application.Commons.Searching.SmartSearch.For(query, fields => [
            fields.Code("colourCode", x => x.ColorCode, "ColorCode", "Mã màu"),
            fields.Name("productName", x => x.ProductName, "Tên sản phẩm", "Product name"),
            fields.Code("productionOrderCode", x => x.MfgProductionOrder == null ? "" : x.MfgProductionOrder.ExternalId, "Mã lệnh SX", "Production order"),
            fields.Code("formulaCode", x => x.ManufacturingFormula == null ? "" : x.ManufacturingFormula.ExternalId, "Công thức", "Formula")
        ]);
        query = smartSearch.ApplyRequest(query, r.Keyword, r, out var smartKeyword);
        if (!string.IsNullOrWhiteSpace(smartKeyword))
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(smartKeyword.Trim());
            query = query.Where(x => EF.Functions.ILike(x.ColorCode ?? "", pattern) || EF.Functions.ILike(x.ProductName ?? "", pattern)
                || EF.Functions.ILike(x.Machine ?? "", pattern)
                || EF.Functions.ILike(x.Resin ?? "", pattern)
                || EF.Functions.ILike(x.Note ?? "", pattern)
                || x.MfgProductionOrder != null && EF.Functions.ILike(x.MfgProductionOrder.ExternalId, pattern)
                || x.ManufacturingFormula != null && EF.Functions.ILike(x.ManufacturingFormula.ExternalId, pattern));
        }

        if (await smartSearch.SuggestionsAsync<GetColorChipManufacturingRecordSummary>(query, r, ct) is { } suggestions)
            return OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>.Ok(suggestions);
        var count = await query.CountAsync(ct);
        var data = await query.OrderByDescending(x => x.RecordDate ?? x.CreatedDate).ThenByDescending(x => x.ColorChipMfgRecordId)
            .Skip((int)skip)
            .Take(r.PageSize)
            .Select(x => new GetColorChipManufacturingRecordSummary
        {
            ColorChipMfgRecordId = x.ColorChipMfgRecordId,
            ColorCode = x.ColorCode,
            ProductName = x.ProductName,
            MfgProductionOrderId = x.MfgProductionOrderId,
            MfgProductionOrderExternalId = x.MfgProductionOrder == null ? null : x.MfgProductionOrder.ExternalId,
            ManufacturingFormulaId = x.ManufacturingFormulaId,
            ManufacturingFormulaExternalId = x.ManufacturingFormula == null ? null : x.ManufacturingFormula.ExternalId,
            ResinType = x.ResinType,
            LogoType = x.LogoType,
            FormStyle = x.FormStyle,
            RecordDate = x.RecordDate,
            Machine = x.Machine,
            UsageRate = x.UsageRate,
            IsActive = x.IsActive
        })
            .ToListAsync(ct);
        return OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>.Ok(new PagedResult<GetColorChipManufacturingRecordSummary>(data, count, r.PageNumber, r.PageSize));
    }

    public async Task<OperationResult<byte[]>> PrintAsync(Guid id, CancellationToken ct)
    {
        var result = await GetAsync(id, ct);
        if (!result.Success || result.Data is null)
        {
            return OperationResult<byte[]>.Fail(result.Message ?? "Không tìm thấy color-chip.");
        }

        var data = result.Data;
        var order = data.MfgProductionOrderId.HasValue ? await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == data.MfgProductionOrderId && x.CompanyId == Company, ct) : null;
        var prepared = user.EmployeeId.HasValue ? await db.Employees.AsNoTracking().Where(x => x.EmployeeId == user.EmployeeId && x.CompanyId == Company)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(ct) : null;
        var model = new ColorChipRecordPdfModel

        {

            BatchNo = data.ManufacturingFormulaExternalId ?? data.MfgProductionOrderExternalId ?? string.Empty,

            Date = data.RecordDate ?? data.CreatedDate,

            Customer = order?.CustomerNameSnapshot ?? string.Empty,

            Code = !string.IsNullOrWhiteSpace(data.ColorCode) ? data.ColorCode : order?.ProductExternalIdSnapshot ?? string.Empty,

            Color = !string.IsNullOrWhiteSpace(data.ProductName) ? data.ProductName : order?.ProductNameSnapshot ?? string.Empty,

            AddRate = data.UsageRate.HasValue ? $"{data.UsageRate.Value.ToString(CultureInfo.CurrentCulture)}%" : string.Empty,

            Resin = !string.IsNullOrWhiteSpace(data.Resin) ? data.Resin : data.ResinType.ToString(),

            PreparedBy = prepared ?? string.Empty,

            StandardText = data.StandardFormula ?? string.Empty,

            Machine = data.Machine,

            TemperatureLimit = data.TemperatureLimit,

            SizeText = data.SizeText,

            PelletWeightGram = data.PelletWeightGram,

            NetWeightGram = data.NetWeightGram,

            Electrostatic = data.Electrostatic,

            Note = data.Note,

            PrintNote = data.PrintNote,

            DeltaE = data.DeltaE,

            ResinTypeText = data.ResinType.ToString(),

            LogoTypeText = data.LogoType.ToString(),

            FormStyleText = data.FormStyle.ToString(),

            DevelopmentFormulaCodes = string.IsNullOrWhiteSpace(data.ManufacturingFormulaExternalId) ? [] : [data.ManufacturingFormulaExternalId]

        };
        // Legacy Manufacturing supports these five styles; other defined styles use landscape.
        var style = data.FormStyle is FormStyle.Chips2 or FormStyle.Chips2_NonStandard or FormStyle.Chips3 or FormStyle.ChipsTanPhu or FormStyle.ChipsTanPhuBacNinh ? data.FormStyle : FormStyle.Chips3;
        try
        {
            var bytes = renderer.Render(model, style);
            return bytes.Length == 0 ? OperationResult<byte[]>.Fail("Không thể tạo PDF color-chip sản xuất.") : OperationResult<byte[]>.Ok(bytes);
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            return OperationResult<byte[]>.Fail("Không thể tạo PDF color-chip sản xuất.");
        }
    }

    private static IQueryable<GetColorChipManufacturingRecord> Map(IQueryable<ColorChipManufacturingRecord> query) => query.Select(x => new GetColorChipManufacturingRecord
    {
        ColorChipMfgRecordId = x.ColorChipMfgRecordId,
        ColorCode = x.ColorCode,
        ProductName = x.ProductName,
        ResinType = x.ResinType,
        LogoType = x.LogoType,
        FormStyle = x.FormStyle,
        MfgProductionOrderId = x.MfgProductionOrderId,
        MfgProductionOrderExternalId = x.MfgProductionOrder == null ? null : x.MfgProductionOrder.ExternalId,
        ManufacturingFormulaId = x.ManufacturingFormulaId,
        ManufacturingFormulaExternalId = x.ManufacturingFormula == null ? null : x.ManufacturingFormula.ExternalId,
        ManufacturingFormulaName = x.ManufacturingFormula == null ? null : x.ManufacturingFormula.Name,
        StandardFormula = x.StandardFormula,
        Machine = x.Machine,
        Resin = x.Resin,
        TemperatureLimit = x.TemperatureLimit,
        SizeText = x.SizeText,
        PelletWeightGram = x.PelletWeightGram,
        NetWeightGram = x.NetWeightGram,
        Electrostatic = x.Electrostatic,
        DeltaE = x.DeltaE,
        UsageRate = x.UsageRate,
        RecordDate = x.RecordDate,
        Note = x.Note,
        PrintNote = x.PrintNote,
        CreatedDate = x.CreatedDate,
        CreatedBy = x.CreatedBy,
        UpdatedDate = x.UpdatedDate,
        UpdatedBy = x.UpdatedBy,
        CompanyId = x.CompanyId,
        IsActive = x.IsActive
    });
    private static OperationResult<GetColorChipManufacturingRecord> Fail(string message) => OperationResult<GetColorChipManufacturingRecord>.Fail(message);
}
