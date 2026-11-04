using System.Security.Cryptography;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;

/// <summary>Đồng bộ result đã lưu từ catalog theo company; validate toàn bộ trước khi sửa, giữ Value/tệp/thứ tự.</summary>
internal sealed class IqcResultCatalogSyncService(IEvaluationDbContext db, EvaluationAccessService access,
    KeyedMutationLock<string> mutationLock)
{
    private const int MaxQcCount = 1000;
    private const int MaxResultCount = 10000;
    private const int PreviewLimit = 200;

    public async Task<IqcResultCatalogSyncDto> PreviewAsync(IqcResultCatalogSyncRequest request, CancellationToken ct)
    {
        var company = await CompanyAsync(ct);
        var plan = await BuildPlanAsync(request, company, false, ct);
        return Reply(plan, false);
    }

    public async Task<IqcResultCatalogSyncDto> ApplyAsync(IqcResultCatalogSyncRequest request, string token, CancellationToken ct)
    {
        var company = await CompanyAsync(ct);
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
            throw new EvaluationRequestException("Cần previewToken từ API xem trước.");
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var plan = await BuildPlanAsync(request, company, true, ct);
        if (!string.Equals(token, plan.Token, StringComparison.Ordinal))
            throw new EvaluationRequestException("Dữ liệu hoặc phạm vi đã thay đổi. Hãy xem trước lại.", 409);
        if (plan.Issues.Count > 0)
            throw new EvaluationRequestException("Có kết quả không tương thích. Xem trước để kiểm tra từng dòng; chưa đồng bộ phiếu nào.", 409);
        foreach (var change in plan.Changes)
        {
            var row = change.Row;
            var after = change.Dto.After;
            EvaluationConfiguration.Apply(row, after.Configuration);
            row.EvaluationStandardId = after.EvaluationStandardId;
            row.ExternalId = after.ExternalId;
            row.CriterionCode = after.CriterionCode;
            row.CriterionName = after.CriterionName;
            row.CriterionNameEn = after.CriterionNameEn;
            row.HasConfigurationSnapshot = true;
        }
        if (plan.Changes.Count > 0) await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return Reply(plan, true);
    }

    private async Task<Guid> CompanyAsync(CancellationToken ct)
    {
        var company = access.CatalogSyncCompany();
        await access.EmployeeAsync(company, ct);
        return company;
    }

    private async Task<Plan> BuildPlanAsync(IqcResultCatalogSyncRequest request, Guid company, bool tracked, CancellationToken ct)
    {
        var (scope, ids, codes) = ValidateSelection(request);
        var qcQuery = db.QCInputByQCs.Where(x => x.WarehouseVoucherDetail.Voucher.CompanyId == company);
        if (scope != "COMPANY")
        {
            qcQuery = qcQuery.Where(x => ids.Contains(x.QCInputByQCId));
            if (await qcQuery.CountAsync(ct) != ids.Length)
                throw new EvaluationRequestException("Có phiếu QC không tồn tại hoặc ngoài công ty hiện tại.", 404);
        }
        // Query scope từ QC cha, không tin company/source ID trên payload.
        var qcIdsQuery = qcQuery.Select(x => x.QCInputByQCId);
        var query = db.IqcInspectionEvaluations.Where(x => qcIdsQuery.Contains(x.QCInputByQCId));
        if (!tracked) query = query.AsNoTracking();
        var evaluations = await query.Include(x => x.Results).ThenInclude(x => x.IqcSpecificationItem)
            .ThenInclude(x => x!.IqcSpecification).OrderBy(x => x.Id).Take(MaxQcCount + 1).ToListAsync(ct);
        if (evaluations.Count > MaxQcCount || evaluations.Sum(x => x.Results.Count) > MaxResultCount)
            throw new EvaluationRequestException("Phạm vi vượt 1000 phiếu hoặc 10000 kết quả. Hãy chia danh sách phiếu được chọn.", 409);
        foreach (var evaluation in evaluations) IqcEvaluationMapper.ValidateLegacy(evaluation, company);
        var allRows = evaluations.SelectMany(e => e.Results.Select(r => (Evaluation: e, Row: r,
            Before: IqcEvaluationMapper.ToResultDto(r)))).OrderBy(x => x.Row.Id).ToList();
        var standardIds = allRows.Select(x => x.Before.EvaluationStandardId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var standardsQuery = db.EvaluationStandards.AsNoTracking().Where(x => x.CompanyId == company);
        standardsQuery = request.AllCriteria ? standardsQuery.Where(x => standardIds.Contains(x.Id))
            : standardsQuery.Where(x => codes.Contains(x.Code.ToUpper()));
        var standards = await standardsQuery.ToListAsync(ct);
        if (!request.AllCriteria && codes.Any(code => !standards.Any(x => x.Code.Trim().Equals(code, StringComparison.OrdinalIgnoreCase))))
            throw new EvaluationRequestException("Không tìm thấy mã chỉ tiêu trong company hiện tại.", 404);
        var selectedIds = standards.Select(x => x.Id).ToHashSet();
        var selected = allRows.Where(x => request.AllCriteria || x.Before.EvaluationStandardId is { } id && selectedIds.Contains(id)).ToList();
        var collections = selected.Where(x => x.Row.AttachmentCollectionId.HasValue)
            .Select(x => x.Row.AttachmentCollectionId!.Value).Distinct().ToArray();
        var files = await db.AttachmentModels.AsNoTracking().Where(x => collections.Contains(x.AttachmentCollectionId) && x.IsActive)
            .OrderBy(x => x.AttachmentId).ToListAsync(ct);
        var sourceItems = new Dictionary<Guid, EvaluationTemplateItem>();
        var sourceErrors = new Dictionary<Guid, string>();
        // Mỗi nguồn chỉ lookup/validate một lần dù có nhiều phiếu dùng cùng chỉ tiêu.
        foreach (var source in standards)
        {
            try
            {
                if (!source.IsActive) throw new EvaluationRequestException("Chỉ tiêu nguồn đã ngừng sử dụng.");
                access.ValidateName(source.Code, source.Name);
                var method = await access.MethodAsync(source.MeasurementMethodId, company, ct);
                var item = EvaluationSnapshotFactory.CreateTemplateItem(source, new EvaluationTemplate { CompanyId = company }, method);
                item.TestMethod = source.TestMethod;
                EvaluationConfiguration.Merge(EvaluationConfiguration.Read(item), new(), null);
                sourceItems.Add(source.Id, item);
            }
            catch (EvaluationRequestException error) { sourceErrors.Add(source.Id, error.Message); }
        }
        var changes = new List<Change>();
        var issues = new List<IqcResultCatalogSyncIssueDto>();
        foreach (var entry in selected)
        {
            var before = entry.Before;
            try
            {
                var source = standards.SingleOrDefault(x => x.Id == before.EvaluationStandardId)
                    ?? throw new EvaluationRequestException("Dòng thiếu nguồn EvaluationStandard trong company.");
                if (sourceErrors.TryGetValue(source.Id, out var sourceError)) throw new EvaluationRequestException(sourceError);
                var item = sourceItems[source.Id];
                var config = EvaluationConfiguration.Read(item) with { SortOrder = before.Configuration.SortOrder };
                if (config.SortOrder < 0) throw new EvaluationRequestException("SortOrder hiện tại không hợp lệ.");
                EvaluationValueRules.Normalize(before.Value, config); // Chỉ validate, giữ nguyên chuỗi giá trị đo.
                ValidateFiles(config, files.Where(x => x.AttachmentCollectionId == entry.Row.AttachmentCollectionId).ToList());
                var after = before with { EvaluationStandardId = source.Id, ExternalId = item.CriterionCode,
                    CriterionCode = item.CriterionCode, CriterionName = item.CriterionName, CriterionNameEn = item.CriterionNameEn,
                    Configuration = config, HasConfigurationSnapshot = true };
                if (before != after) changes.Add(new(entry.Row, new(entry.Evaluation.QCInputByQCId, before, after)));
            }
            catch (EvaluationRequestException error)
            {
                issues.Add(new(entry.Evaluation.QCInputByQCId, entry.Row.Id, before.CriterionCode, error.Message));
            }
        }
        // Unique ExternalId và CriterionCode trên từng phiếu phải được bảo toàn.
        var replacements = changes.ToDictionary(x => x.Row.Id, x => x.Dto.After);
        foreach (var group in allRows.GroupBy(x => x.Evaluation.Id))
        {
            var final = group.Select(x => replacements.GetValueOrDefault(x.Row.Id, x.Before)).ToList();
            if (final.Select(x => x.ExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != final.Count ||
                final.Select(x => x.CriterionCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != final.Count)
                issues.Add(new(group.First().Evaluation.QCInputByQCId, Guid.Empty, "", "Mã chỉ tiêu sau đồng bộ bị trùng trên phiếu."));
        }
        var hashData = new { Company = company, Scope = scope, Ids = ids.Order().ToArray(), Codes = codes.Order().ToArray(), request.AllCriteria,
            Rows = allRows.OrderBy(x => x.Row.Id).Select(x => new { x.Evaluation.Id, x.Evaluation.QCInputByQCId, x.Before }),
            Sources = standards.OrderBy(x => x.Id).Select(x => new { x.Id, x.Code, x.Name, x.NameEn, x.IsActive, x.DataType, x.Unit,
                x.MeasurementMethodId, x.TestMethod, x.InputConfigJson, x.AcceptanceRuleJson, x.SpecificationText, x.IsRequired }), Files = files.Select(x => new { x.AttachmentId, x.AttachmentCollectionId, x.FileName, x.SizeBytes, x.Slot }),
            Changes = changes.Select(x => x.Dto), Issues = issues };
        var token = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(hashData)));
        return new(token, evaluations.Count, selected.Count, changes, issues);
    }

    private static (string Scope, Guid[] Ids, string[] Codes) ValidateSelection(IqcResultCatalogSyncRequest request)
    {
        if (request is null) throw new EvaluationRequestException("selection không được null.");
        var scope = request.Scope?.Trim().ToUpperInvariant();
        if (scope is not ("CURRENT" or "SELECTED" or "COMPANY")) throw new EvaluationRequestException("scope phải là Current, Selected hoặc Company.");
        var ids = request.QcInputByQCIds?.ToArray() ?? [];
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length || ids.Length > MaxQcCount ||
            scope == "CURRENT" && ids.Length != 1 || scope == "SELECTED" && ids.Length == 0 || scope == "COMPANY" && ids.Length != 0)
            throw new EvaluationRequestException("Danh sách QCInputByQCIds không hợp lệ cho scope.");
        var rawCodes = request.Codes ?? [];
        if (request.AllCriteria && rawCodes.Count > 0 || !request.AllCriteria && rawCodes.Count is < 1 or > 200 ||
            rawCodes.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 50))
            throw new EvaluationRequestException("Chọn codes (1..200 mã) hoặc allCriteria=true, không dùng đồng thời.");
        var codes = rawCodes.Select(x => x.Trim().ToUpperInvariant()).ToArray();
        if (codes.Distinct().Count() != codes.Length) throw new EvaluationRequestException("codes chứa mã trùng.");
        return (scope, ids, codes);
    }

    private static void ValidateFiles(HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos.EvaluationConfigurationDto config,
        List<HRM.Domain.Entities.AttachmentSchema.AttachmentModel> files)
    {
        if (files.Count > 0 && (config.DataType != EvaluationDataType.Attachment || files.Any(x => x.Slot != AttachmentSlot.EvaluationAttachment)))
            throw new EvaluationRequestException("Không thể giữ tệp với kiểu dữ liệu hoặc slot mới này.");
        if (config.DataType != EvaluationDataType.Attachment) return;
        using var json = config.InputConfigJson is null ? null : JsonDocument.Parse(config.InputConfigJson);
        var root = json?.RootElement;
        var maxFiles = root is { } r && r.TryGetProperty("maxFiles", out var count) ? count.GetInt32() : 20;
        var maxBytes = root is { } s && s.TryGetProperty("maxFileSizeBytes", out var bytes) ? bytes.GetInt64() : 10000000;
        var extensions = root is { } t && t.TryGetProperty("allowedExtensions", out var list)
            ? list.EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        if (files.Count > maxFiles || config.IsRequired && files.Count == 0 ||
            files.Any(x => x.SizeBytes > maxBytes || extensions is not null && !extensions.Contains(Path.GetExtension(x.FileName))))
            throw new EvaluationRequestException("Tệp hiện có không đáp ứng cấu hình mới (bắt buộc/số lượng/dung lượng/định dạng).");
    }

    private static IqcResultCatalogSyncDto Reply(Plan plan, bool applied) => new(plan.Token, plan.Issues.Count == 0, applied,
        plan.QcCount, plan.ResultCount, plan.Changes.Select(x => x.Dto.QcInputByQCId).Distinct().Count(), plan.Changes.Count,
        plan.Issues.Count, plan.Changes.Take(PreviewLimit).Select(x => x.Dto).ToList(), plan.Issues.Take(PreviewLimit).ToList(),
        plan.Changes.Count > PreviewLimit, plan.Issues.Count > PreviewLimit);
    private sealed record Change(IqcInspectionResult Row, IqcResultCatalogSyncChangeDto Dto);
    private sealed record Plan(string Token, int QcCount, int ResultCount, List<Change> Changes, List<IqcResultCatalogSyncIssueDto> Issues);
}
