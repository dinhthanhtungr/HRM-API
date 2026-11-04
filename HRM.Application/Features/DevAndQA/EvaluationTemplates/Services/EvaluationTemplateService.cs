using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Patching;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.GetEvaluationTemplates;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.CreateEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PatchEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PublishEvaluationTemplate;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;

/// <summary>Quản lý template: tạo nháp, sửa cấu hình, phát hành mặc định và ngưng sử dụng.</summary>
internal sealed partial class EvaluationTemplateService(IEvaluationDbContext db,
    EvaluationAccessService access,
    KeyedMutationLock<string> mutationLock)
{
    /// <summary>Danh sách template theo loại/trạng thái người dùng có quyền xem.</summary>
    public async Task<PagedResult<EvaluationTemplateDto>> ListTemplatesAsync(GetEvaluationTemplatesQuery request, CancellationToken ct)
    {
        var company = access.Company(false, request.Type);
        if (request.Type.HasValue && !Enum.IsDefined(request.Type.Value) || request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
            throw new EvaluationRequestException("Type/Status không hợp lệ.");
        var iqc = access.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc);
        var coa = access.HasPermission(ApplicationPermissions.DevAndQA.ViewCoa);
        var query = db.EvaluationTemplates.AsNoTracking().Include(x => x.Items).Where(x => x.CompanyId == company &&
            (x.Type == EvaluationTemplateType.Coa ? coa : iqc));
        if (request.Type.HasValue) query = query.Where(x => x.Type == request.Type.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        var skip = access.PageOffset(request.PageNumber, request.PageSize);
        var total = await query.CountAsync(ct);
        var items = (await query.OrderBy(x => x.Code).ThenByDescending(x => x.Version).ThenBy(x => x.Id).Skip(skip).Take(request.PageSize).ToListAsync(ct)).Select(TemplateDto).ToList();
        return new(items, total, request.PageNumber, request.PageSize);
    }

    private async Task<EvaluationTemplate> LoadTemplateAsync(Guid id, Guid company, CancellationToken ct) =>
        await db.EvaluationTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == company, ct)
        ?? throw new EvaluationRequestException("Không tìm thấy template.", 404);

    /// <summary>Chi tiết template và các dòng, kiểm tra quyền theo Type.</summary>
    public async Task<EvaluationTemplateDto> GetTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await LoadTemplateAsync(id, access.Company(), ct);
        access.Company(false, template.Type);
        return TemplateDto(template);
    }

    /// <summary>Lấy mẫu mặc định đã phát hành, đúng loại và có hiệu lực.</summary>
    public async Task<EvaluationTemplateDto> GetDefaultTemplateAsync(EvaluationTemplateType type, CancellationToken ct) =>
        TemplateDto(await SelectTemplateAsync(null, type, access.Company(false, type), ct));

    internal async Task<EvaluationTemplate> SelectTemplateAsync(Guid? id, EvaluationTemplateType type, Guid company, CancellationToken ct)
    {
        if (!Enum.IsDefined(type)) throw new EvaluationRequestException("Loại template không hợp lệ.");
        var now = DateTime.Now;
        var query = db.EvaluationTemplates.Include(x => x.Items).Where(x => x.CompanyId == company && x.Type == type &&
            x.Status == EvaluationTemplateStatus.Published && (x.EffectiveFrom == null || x.EffectiveFrom <= now));
        var template = await (id.HasValue ? query.Where(x => x.Id == id.Value) : query.Where(x => x.IsDefault)).FirstOrDefaultAsync(ct)
            ?? throw new EvaluationRequestException("Chưa có template đã phát hành phù hợp.", 404);
        if (template.Items.Count == 0) throw new EvaluationRequestException("Template chưa có chỉ tiêu.");
        var standardIds = template.Items.Select(x => x.EvaluationStandardId).Distinct().ToList();
        var methodIds = template.Items.Where(x => x.MeasurementMethodId.HasValue).Select(x => x.MeasurementMethodId!.Value).Distinct().ToList();
        if (await db.EvaluationStandards.CountAsync(x => standardIds.Contains(x.Id) && x.CompanyId == company, ct) != standardIds.Count ||
            await db.MeasurementMethods.CountAsync(x => methodIds.Contains(x.Id) && x.CompanyId == company, ct) != methodIds.Count)
            throw new EvaluationRequestException("Template chứa liên kết ngoài công ty.");
        foreach (var item in template.Items)
            EvaluationConfiguration.Merge(EvaluationConfiguration.Read(item), new(), null);

        return template;
    }

    private async Task<List<EvaluationTemplateItem>> BuildItemsAsync(EvaluationTemplate template, IReadOnlyList<EvaluationTemplateItemRequest> requests, CancellationToken ct)
    {
        if (requests is null || requests.Count > 200 || requests.Any(x => x is null) || requests.Select(x => x.EvaluationStandardId).Distinct().Count() != requests.Count)
            throw new EvaluationRequestException("Danh sách chỉ tiêu không hợp lệ, trùng hoặc vượt 200 dòng.");
        var items = new List<EvaluationTemplateItem>();
        foreach (var request in requests)
        {
            var standard = await access.StandardAsync(request.EvaluationStandardId, template.CompanyId, ct);
            var methodId = request.Configuration?.ClearFields?.Contains("measurementMethodId") == true ? null : request.Configuration?.MeasurementMethodId ?? standard.MeasurementMethodId;
            var method = await access.MethodAsync(methodId, template.CompanyId, ct);
            // Load default method even when the override explicitly clears it: factory validates the source defaults.
            var defaultMethod = await access.MethodAsync(standard.MeasurementMethodId, template.CompanyId, ct);
            var item = EvaluationSnapshotFactory.CreateTemplateItem(standard,
                new EvaluationTemplate { Id = template.Id, CompanyId = template.CompanyId }, defaultMethod);
            var configRequest = request.Configuration ?? new EvaluationConfigurationRequest();
            var config = EvaluationConfiguration.Merge(EvaluationConfiguration.Read(item), configRequest, method);
            EvaluationConfiguration.Apply(item, config);
            item.SortOrder = items.Count + 1;
            items.Add(item);
        }
        return items;
    }

    /// <summary>Tạo phiên bản Draft và copy cấu hình mặc định từ danh mục.</summary>
    public async Task<EvaluationTemplateDto> CreateTemplateAsync(CreateEvaluationTemplateCommand request, CancellationToken ct)
    {
        var company = access.Company(true, request.Type);
        if (!access.CanManageTemplateDefinition)
        {
            throw new EvaluationRequestException("Chỉ Developer có quyền quản lý đúng loại được tạo template.", 403);
        }
        var employee = await access.EmployeeAsync(company, ct);
        access.ValidateName(request.Code, request.Name);
        if (!Enum.IsDefined(request.Type) || request.Version < 1) throw new EvaluationRequestException("Type/Version không hợp lệ.");
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var code = request.Code.Trim();
        if (await db.EvaluationTemplates.AnyAsync(x => x.CompanyId == company && x.Type == request.Type && x.Code.ToUpper() == code.ToUpper() && x.Version == request.Version, ct))
            throw new EvaluationRequestException("Phiên bản template đã tồn tại.", 409);
        var template = new EvaluationTemplate
        {
            Id = Guid.CreateVersion7(), CompanyId = company, Code = code, Name = request.Name.Trim(),
            Type = request.Type, Version = request.Version, CreatedBy = employee, CreatedDate = DateTime.Now
        };
        template.Items = await BuildItemsAsync(template, request.Items, ct);
        db.EvaluationTemplates.Add(template);
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return TemplateDto(template);
    }

    /// <summary>Developer/QCUser có capability đúng loại sửa nội dung mọi trạng thái; chỉ Developer đổi mã, không thay snapshot phiếu cũ.</summary>
    public async Task<EvaluationTemplateDto> PatchTemplateAsync(PatchEvaluationTemplateCommand request, CancellationToken ct)
    {
        // Ngoại lệ role-only chỉ áp dụng request đồng bộ thuần; không mở quyền sửa field khác.
        var syncOnly = request.SyncStandardCodes is not null && request.Code is null && request.Name is null && request.Items is null;
        if (request.SyncStandardCodes is not null) access.CatalogSyncCompany();
        var company = syncOnly ? access.CatalogSyncCompany() : access.Company(true);
        await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var template = await LoadTemplateAsync(request.Id, company, ct);
        if (!syncOnly) access.Company(true, template.Type);
        if (template.Status != EvaluationTemplateStatus.Draft && !access.CanEditNonDraftTemplate)
            throw new EvaluationRequestException("Chỉ sửa template nháp; template đã phát hành phải tạo phiên bản mới.", 409);
        var code = request.Code?.Trim() ?? template.Code;
        if (!access.CanManageTemplateDefinition && code != template.Code)
        {
            throw new EvaluationRequestException("Chỉ Developer được đổi mã template.", 403);
        }
        var name = request.Name?.Trim() ?? template.Name;
        access.ValidateName(code, name);
        if (await db.EvaluationTemplates.AnyAsync(x => x.CompanyId == company && x.Id != template.Id && x.Type == template.Type && x.Version == template.Version && x.Code.ToUpper() == code.ToUpper(), ct))
            throw new EvaluationRequestException("Phiên bản template đã tồn tại.", 409);
        var items = request.Items is null ? null : await BuildPatchItemsAsync(template, request.Items, ct);
        var finalItems = items ?? template.Items.ToList();
        var syncPlan = await BuildCatalogSyncPlanAsync(template, finalItems, request.SyncStandardCodes, ct);
        var changed = PatchHelper.Set(code, () => template.Code, x => template.Code = x);
        changed |= PatchHelper.Set(name, () => template.Name, x => template.Name = x);
        if (items is not null)
        {
            // Giữ ID của chỉ tiêu còn lại; ID nguồn trong snapshot cũ chỉ là dấu vết tham khảo.
            var existing = template.Items.ToDictionary(x => x.EvaluationStandardId);
            var keep = items.Select(x => x.EvaluationStandardId).ToHashSet();
            db.EvaluationTemplateItems.RemoveRange(template.Items.Where(x => !keep.Contains(x.EvaluationStandardId)).ToList());
            template.Items = items.Select(item =>
            {
                if (!existing.TryGetValue(item.EvaluationStandardId, out var current))
                {
                    // GUID đã được factory gán; Add tường minh để EF INSERT thay vì coi là dòng cũ.
                    db.EvaluationTemplateItems.Add(item);
                    return item;
                }
                current.CriterionCode = item.CriterionCode;
                current.CriterionName = item.CriterionName;
                current.CriterionNameEn = item.CriterionNameEn;
                EvaluationConfiguration.Apply(current, EvaluationConfiguration.Read(item));
                return current;
            }).ToList();
            changed = true;
        }
        foreach (var (target, source) in syncPlan)
        {
            var current = template.Items.Single(x => x.EvaluationStandardId == target.EvaluationStandardId);
            current.CriterionCode = source.CriterionCode;
            current.CriterionName = source.CriterionName;
            current.CriterionNameEn = source.CriterionNameEn;
            EvaluationConfiguration.Apply(current, EvaluationConfiguration.Read(source) with { SortOrder = current.SortOrder });
            changed = true;
        }
        if (changed) await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return TemplateDto(template);
    }

    /// <summary>Phát hành template và cập nhật mẫu mặc định trong transaction.</summary>
    public async Task<EvaluationTemplateDto> PublishTemplateAsync(PublishEvaluationTemplateCommand request, CancellationToken ct)
    {
        var company = access.Company(true);
        var employee = await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var template = await LoadTemplateAsync(request.Id, company, ct);
        access.Company(true, template.Type);
        if (template.Status != EvaluationTemplateStatus.Draft || template.Items.Count == 0)
            throw new EvaluationRequestException("Chỉ phát hành template nháp có ít nhất một chỉ tiêu.", 409);
        foreach (var item in template.Items)
        {
            await access.StandardAsync(item.EvaluationStandardId, company, ct);
            await access.MethodAsync(item.MeasurementMethodId, company, ct);
            EvaluationConfiguration.Merge(EvaluationConfiguration.Read(item), new(), null);
        }
        if (request.IsDefault)
        {
            var oldDefaults = await db.EvaluationTemplates.Where(x => x.CompanyId == company && x.Type == template.Type && x.IsDefault).ToListAsync(ct);
            foreach (var old in oldDefaults) old.IsDefault = false;
            if (oldDefaults.Count != 0) await access.SaveAsync(ct);
        }
        template.Status = EvaluationTemplateStatus.Published;
        template.IsDefault = request.IsDefault;
        template.EffectiveFrom = DateTime.Now;
        template.PublishedBy = employee;
        template.PublishedDate = DateTime.Now;
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return TemplateDto(template);
    }

    /// <summary>Ngưng template, giữ nguyên snapshot trên các phiếu đã lưu.</summary>
    public async Task<EvaluationTemplateDto> RetireTemplateAsync(Guid id, CancellationToken ct)
    {
        var company = access.Company(true);
        await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var template = await LoadTemplateAsync(id, company, ct);
        access.Company(true, template.Type);
        if (template.Status == EvaluationTemplateStatus.Draft) throw new EvaluationRequestException("Template chưa phát hành.", 409);
        if (template.Status != EvaluationTemplateStatus.Retired)
        {
            template.Status = EvaluationTemplateStatus.Retired;
            template.IsDefault = false;
            await access.SaveAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return TemplateDto(template);
    }
    private static EvaluationTemplateDto TemplateDto(EvaluationTemplate x) => new(x.Id, x.Code, x.Name, x.Type, x.Version, x.Status, x.IsDefault, x.EffectiveFrom,
        x.CreatedBy, x.CreatedDate, x.PublishedBy, x.PublishedDate,
        x.Items.OrderBy(i => i.SortOrder).Select(i => new EvaluationTemplateItemDto(i.Id, i.EvaluationStandardId, i.CriterionCode, i.CriterionName, EvaluationConfiguration.Read(i), i.CriterionNameEn)).ToList());

}
