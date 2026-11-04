using System.Globalization;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.LabTests.Services;

/// <summary>CRUD LabTest dùng quyền IQC, company scope, snapshot riêng và transaction Serializable.</summary>
internal sealed partial class LabTestService(ILabTestDbContext db, EvaluationAccessService access,
    KeyedMutationLock<string> mutationLock)
{
    private const string CodePrefix = "NVL_TEST_";

    public async Task<LabTestDto> DetailAsync(Guid id, CancellationToken ct)
    {
        var company = access.Company(type: EvaluationTemplateType.Iqc);
        return LabTestMapper.ToDto(await FindAsync(id, company, false, ct));
    }

    public async Task<LabTestDto> CreateAsync(LabTestWriteRequest request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        var employee = await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = await StageCreateAsync(request, company, employee, await NextCodeAsync(company, ct), ct);
        db.LabTests.Add(entity);
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return LabTestMapper.ToDto(entity);
    }

    public async Task<LabTestDto> PatchAsync(Guid id, LabTestWriteRequest request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        var employee = await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = await FindAsync(id, company, true, ct);
        if (request.EvaluationTemplateId is { } templateId && templateId != entity.EvaluationTemplateId)
            throw new EvaluationRequestException("Không đổi template của phiếu đã lưu.", 409);
        LabTestMetadataRules.Apply(entity, request, false);
        await StageResultsAsync(entity, request.Results, company, ct, true);
        entity.UpdatedBy = employee;
        entity.UpdatedDate = DateTime.Now;
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return LabTestMapper.ToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        var employee = await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = await db.LabTests.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == company, ct)
            ?? throw new EvaluationRequestException("Không tìm thấy phiếu test.", 404);
        if (!entity.IsDeleted)
        {
            entity.IsDeleted = true;
            entity.UpdatedBy = employee;
            entity.UpdatedDate = DateTime.Now;
            await access.SaveAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return true;
    }

    private async Task<LabTest> FindAsync(Guid id, Guid company, bool tracking, CancellationToken ct)
    {
        var query = db.LabTests.Where(x => x.Id == id && x.CompanyId == company && !x.IsDeleted);
        if (!tracking) query = query.AsNoTracking();
        return await query.Include(x => x.Results).FirstOrDefaultAsync(ct)
            ?? throw new EvaluationRequestException("Không tìm thấy phiếu test.", 404);
    }

    private async Task<LabTest> StageCreateAsync(LabTestWriteRequest request, Guid company,
        Guid employee, string code, CancellationToken ct, IReadOnlyDictionary<Guid, EvaluationTemplateItem>? catalog = null)
    {
        var entity = new LabTest { Id = Guid.CreateVersion7(), Code = code, CompanyId = company,
            CreatedBy = employee, CreatedDate = DateTime.Now };
        LabTestMetadataRules.Apply(entity, request, true);
        if (request.EvaluationTemplateId.HasValue)
        {
            var template = await db.EvaluationTemplates.AsNoTracking().Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == request.EvaluationTemplateId && x.CompanyId == company &&
                    x.Type == EvaluationTemplateType.Iqc && x.Status == EvaluationTemplateStatus.Published &&
                    (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= DateTime.Now), ct)
                ?? throw new EvaluationRequestException("Template IQC chưa phát hành/có hiệu lực hoặc ngoài company.");
            if (template.Items.Count > 200 || template.Items.Select(x => x.EvaluationStandardId).Distinct().Count() != template.Items.Count)
                throw new EvaluationRequestException("Template vượt 200 dòng hoặc trùng chỉ tiêu.");
            var standardIds = template.Items.Select(x => x.EvaluationStandardId).ToArray();
            var methodIds = template.Items.Where(x => x.MeasurementMethodId.HasValue).Select(x => x.MeasurementMethodId!.Value).Distinct().ToArray();
            if (await db.EvaluationStandards.CountAsync(x => standardIds.Contains(x.Id) && x.CompanyId == company, ct) != standardIds.Length ||
                await db.MeasurementMethods.CountAsync(x => methodIds.Contains(x.Id) && x.CompanyId == company, ct) != methodIds.Length)
                throw new EvaluationRequestException("Nguồn chỉ tiêu/phương pháp template không thuộc company.");
            entity.EvaluationTemplateId = template.Id;
            entity.TemplateCode = template.Code; entity.TemplateName = template.Name; entity.TemplateVersion = template.Version;
            foreach (var item in template.Items) entity.Results.Add(LabTestMapper.Snapshot(item, entity.Id, true));
        }
        await StageResultsAsync(entity, request.Results, company, ct, catalog: catalog);
        return entity;
    }

    private async Task StageResultsAsync(LabTest entity, IReadOnlyList<LabTestResultInput>? inputs, Guid company, CancellationToken ct,
        bool existing = false, IReadOnlyDictionary<Guid, EvaluationTemplateItem>? catalog = null)
    {
        if (inputs is null) return;
        if (inputs.Count > 200 || inputs.Any(x => x is null || x.EvaluationStandardId == Guid.Empty) ||
            inputs.Select(x => x.EvaluationStandardId).Distinct().Count() != inputs.Count)
            throw new EvaluationRequestException("Kết quả trùng/không hợp lệ hoặc vượt 200 chỉ tiêu.");
        catalog ??= await CatalogSnapshotsAsync(inputs.Where(x => !entity.Results.Any(r => r.EvaluationStandardId == x.EvaluationStandardId))
            .Select(x => x.EvaluationStandardId).ToArray(), company, ct);
        foreach (var input in inputs)
        {
            if (input.ClearValue && input.Value is not null || input.ClearObservedText && input.ObservedText is not null ||
                input.ObservedText?.Length > 16000)
                throw new EvaluationRequestException("Không vừa gửi vừa xóa kết quả; ObservedText tối đa 16000 ký tự.");
            var row = entity.Results.FirstOrDefault(x => x.EvaluationStandardId == input.EvaluationStandardId);
            if (row is null)
            {
                if (!catalog.TryGetValue(input.EvaluationStandardId, out var item))
                    throw new EvaluationRequestException("Chỉ tiêu không hợp lệ hoặc đã ngừng sử dụng.");
                row = LabTestMapper.Snapshot(item, entity.Id, false);
                row.SortOrder = entity.Results.Select(x => x.SortOrder).DefaultIfEmpty(-1).Max() + 1;
                if (entity.Results.Any(x => string.Equals(x.CriterionCode, row.CriterionCode, StringComparison.OrdinalIgnoreCase)))
                    throw new EvaluationRequestException("Trùng mã chỉ tiêu trên phiếu.", 409);
                entity.Results.Add(row);
                if (existing) db.LabTestResults.Add(row);
            }
            if (row.DataType == EvaluationDataType.Attachment && input.ObservedText is not null)
                throw new EvaluationRequestException("Chỉ tiêu Attachment không nhận ObservedText.");
            row.Value = EvaluationValueRules.Normalize(input.ClearValue ? null : input.Value ?? row.Value, LabTestMapper.Configuration(row));
            if (input.ClearObservedText || input.ObservedText is not null)
                row.ObservedText = input.ClearObservedText || string.IsNullOrWhiteSpace(input.ObservedText) ? null : input.ObservedText.Trim();
        }
        if (entity.Results.Count > 200) throw new EvaluationRequestException("Phiếu vượt 200 chỉ tiêu.");
    }

    private async Task<IReadOnlyDictionary<Guid, EvaluationTemplateItem>> CatalogSnapshotsAsync(Guid[] ids, Guid company, CancellationToken ct)
    {
        ids = ids.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, EvaluationTemplateItem>();
        var standards = await db.EvaluationStandards.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.CompanyId == company && x.IsActive).ToListAsync(ct);
        if (standards.Count != ids.Length) throw new EvaluationRequestException("Chỉ tiêu không hợp lệ hoặc đã ngừng sử dụng.");
        var methodIds = standards.Where(x => x.MeasurementMethodId.HasValue).Select(x => x.MeasurementMethodId!.Value).Distinct().ToArray();
        var methods = await db.MeasurementMethods.AsNoTracking().Where(x => methodIds.Contains(x.Id) && x.CompanyId == company && x.IsActive)
            .ToDictionaryAsync(x => x.Id, ct);
        if (methods.Count != methodIds.Length) throw new EvaluationRequestException("Phương pháp đo không hợp lệ hoặc đã ngừng sử dụng.");
        return standards.ToDictionary(x => x.Id, x => EvaluationSnapshotFactory.CreateTemplateItem(x,
            new EvaluationTemplate { CompanyId = company }, x.MeasurementMethodId is { } methodId ? methods[methodId] : null));
    }

    private async Task<string> NextCodeAsync(Guid company, CancellationToken ct)
    {
        var codes = await db.LabTests.Where(x => x.CompanyId == company && x.Code.StartsWith(CodePrefix))
            .Select(x => x.Code).ToListAsync(ct);
        var maximum = codes.Select(x => long.TryParse(x[CodePrefix.Length..], NumberStyles.None,
            CultureInfo.InvariantCulture, out var number) ? number : 0).DefaultIfEmpty().Max();
        if (maximum >= 99999999) throw new EvaluationRequestException("Đã hết dải mã phiếu test.", 409);
        return CodePrefix + (maximum + 1).ToString("D8", CultureInfo.InvariantCulture);
    }
}
