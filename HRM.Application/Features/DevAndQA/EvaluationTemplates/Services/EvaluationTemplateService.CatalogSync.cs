using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.PreviewTemplateCatalogSync;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;

internal sealed partial class EvaluationTemplateService
{
    /// <summary>Xem trước toàn bộ snapshot thay thế, giữ draft FE và không Save/Commit.</summary>
    public async Task<TemplateCatalogSyncPreviewDto> PreviewCatalogSyncAsync(PreviewTemplateCatalogSyncQuery request, CancellationToken ct)
    {
        var company = access.CatalogSyncCompany();
        await access.EmployeeAsync(company, ct);
        var template = await LoadTemplateAsync(request.Id, company, ct);
        if (template.Status != EvaluationTemplateStatus.Draft && !access.CanEditNonDraftTemplate)
            throw new EvaluationRequestException("Không được sửa template ở trạng thái hiện tại.", 409);
        if (request.Codes is null)
            throw new EvaluationRequestException("codes không được null.");
        var items = request.Items is null ? template.Items.ToList() : await BuildPatchItemsAsync(template, request.Items, ct);
        var plan = await BuildCatalogSyncPlanAsync(template, items, request.Codes, ct);
        return new(template.Id, plan.Select(x => new TemplateCatalogSyncChangeDto(x.Source.CriterionCode,
            x.Target.EvaluationStandardId,
            new(x.Target.Id, x.Target.EvaluationStandardId, x.Target.CriterionCode, x.Target.CriterionName,
                EvaluationConfiguration.Read(x.Target), x.Target.CriterionNameEn),
            new(x.Target.Id, x.Target.EvaluationStandardId, x.Source.CriterionCode, x.Source.CriterionName,
                EvaluationConfiguration.Read(x.Source) with { SortOrder = x.Target.SortOrder }, x.Source.CriterionNameEn))).ToList());
    }

    /// <summary>Lưu thường giữ snapshot dòng cũ; chỉ dòng mới lấy mặc định từ danh mục.</summary>
    private async Task<List<EvaluationTemplateItem>> BuildPatchItemsAsync(EvaluationTemplate template,
        IReadOnlyList<EvaluationTemplateItemRequest> requests, CancellationToken ct)
    {
        if (requests.Count > 200 || requests.Any(x => x is null) ||
            requests.Select(x => x.EvaluationStandardId).Distinct().Count() != requests.Count)
            throw new EvaluationRequestException("Danh sách chỉ tiêu không hợp lệ, trùng hoặc vượt 200 dòng.");

        var existing = template.Items.ToDictionary(x => x.EvaluationStandardId);
        var result = new List<EvaluationTemplateItem>();
        foreach (var request in requests)
        {
            if (!existing.TryGetValue(request.EvaluationStandardId, out var current))
            {
                var added = (await BuildItemsAsync(template, [request], ct)).Single();
                added.SortOrder = result.Count + 1;
                result.Add(added);
                continue;
            }

            var configRequest = request.Configuration ?? new EvaluationConfigurationRequest();
            // Không lookup phương pháp đang giữ: inactive không ngăn lưu dòng cũ.
            var method = configRequest.MeasurementMethodId.HasValue &&
                configRequest.MeasurementMethodId != current.MeasurementMethodId
                ? await access.MethodAsync(configRequest.MeasurementMethodId, template.CompanyId, ct) : null;
            var config = EvaluationConfiguration.Merge(EvaluationConfiguration.Read(current), configRequest, method);
            var candidate = new EvaluationTemplateItem
            {
                Id = current.Id, EvaluationTemplateId = template.Id,
                EvaluationStandardId = current.EvaluationStandardId,
                CriterionCode = current.CriterionCode, CriterionName = current.CriterionName,
                CriterionNameEn = current.CriterionNameEn
            };
            EvaluationConfiguration.Apply(candidate, config with { SortOrder = result.Count + 1 });
            result.Add(candidate);
        }
        return result;
    }

    /// <summary>Validate toàn bộ mã trước khi sửa entity; thay cấu hình cả null, giữ thứ tự và danh sách.</summary>
    private async Task<List<(EvaluationTemplateItem Target, EvaluationTemplateItem Source)>> BuildCatalogSyncPlanAsync(
        EvaluationTemplate template, List<EvaluationTemplateItem> finalItems,
        IReadOnlyList<string>? codes, CancellationToken ct)
    {
        var plan = new List<(EvaluationTemplateItem, EvaluationTemplateItem)>();
        if (codes is null) return plan;
        if (codes.Count is < 1 or > 200 || codes.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 50))
            throw new EvaluationRequestException("syncStandardCodes cần từ 1 đến 200 mã hợp lệ.");

        var normalized = codes.Select(x => x.Trim().ToUpperInvariant()).ToArray();
        if (normalized.Distinct().Count() != normalized.Length)
            throw new EvaluationRequestException("syncStandardCodes chứa mã trùng.");

        var standards = await db.EvaluationStandards.Where(x => x.CompanyId == template.CompanyId &&
            normalized.Contains(x.Code.ToUpper())).ToListAsync(ct);
        foreach (var code in normalized)
        {
            var standard = standards.SingleOrDefault(x => string.Equals(x.Code.Trim(), code, StringComparison.OrdinalIgnoreCase))
                ?? throw new EvaluationRequestException("Không tìm thấy chỉ tiêu cần đồng bộ trong công ty hiện tại.", 404);
            if (!standard.IsActive)
                throw new EvaluationRequestException("Chỉ tiêu cần đồng bộ đã ngừng sử dụng.", 409);
            var target = finalItems.SingleOrDefault(x => x.EvaluationStandardId == standard.Id)
                ?? throw new EvaluationRequestException("Chỉ tiêu cần đồng bộ không có trong danh sách mẫu cuối cùng.", 409);
            var method = await access.MethodAsync(standard.MeasurementMethodId, template.CompanyId, ct);
            var source = EvaluationSnapshotFactory.CreateTemplateItem(standard,
                new EvaluationTemplate { Id = template.Id, CompanyId = template.CompanyId }, method);
            // Đồng bộ đúng TestMethod trong catalog, không thay bằng tên phương pháp lookup.
            source.TestMethod = standard.TestMethod;
            EvaluationConfiguration.Merge(EvaluationConfiguration.Read(source), new(), null);
            plan.Add((target, source));
        }
        // Tránh vi phạm unique CriterionCode nếu catalog đổi mã trùng snapshot của dòng khác.
        var finalCodes = finalItems.Select(x => plan.FirstOrDefault(p => p.Item1.EvaluationStandardId == x.EvaluationStandardId).Item2?.CriterionCode ?? x.CriterionCode);
        if (finalCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != finalItems.Count)
            throw new EvaluationRequestException("Mã chỉ tiêu sau đồng bộ bị trùng trong mẫu.", 409);
        return plan;
    }
}
