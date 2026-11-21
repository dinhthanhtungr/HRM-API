using System.Globalization;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

/// <summary>Phiếu test tối giản theo company, dùng snapshot template IQC; không bật import/cột Excel.</summary>
internal sealed partial class BasicLabTestService(ILabTestDbContext db, EvaluationAccessService access, KeyedMutationLock<string> mutationLock)
{
    private const string CodePrefix = "NVL_TEST_";

    public async Task<PagedResult<BasicLabTestDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        var company = access.Company(false, EvaluationTemplateType.Iqc);
        var skip = access.PageOffset(pageNumber, pageSize);
        var query = db.LabTests.AsNoTracking().Where(x => x.CompanyId == company);
        var count = await query.CountAsync(ct);
        var rows = await query.Include(x => x.Results).Include(x => x.Supplier).Include(x => x.CreatedByEmployee).OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id)
            .Skip(skip).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(BasicLabTestMapper.ToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<BasicLabTestDto> DetailAsync(Guid id, CancellationToken ct) =>
        BasicLabTestMapper.ToDto(await FindAsync(id, access.Company(false, EvaluationTemplateType.Iqc), false, ct));

    public async Task<BasicLabTestDto> SaveAsync(Guid? id, BasicLabTestWriteRequest request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        var employee = await access.EmployeeAsync(company, ct);
        Validate(request, !id.HasValue);
        var creator = request.CreatedBy.HasValue
            ? await LabEmployees(company).AsTracking().FirstOrDefaultAsync(x => x.EmployeeId == request.CreatedBy, ct)
            : null;
        if (request.CreatedBy.HasValue && creator is null)
            throw new EvaluationRequestException("Người tạo phải là nhân viên LabUser đang hoạt động trong công ty hiện tại.");
        var supplier = request.SupplierId is { } supplierId
            ? await db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == supplierId && x.CompanyId == company && x.IsActive == true, ct)
            : null;
        if (request.SupplierId.HasValue && supplier is null)
            throw new EvaluationRequestException("NCC không hoạt động hoặc không thuộc công ty hiện tại.");
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = id.HasValue ? await FindAsync(id.Value, company, true, ct) : new LabTest
        {
            Id = Guid.CreateVersion7(), CompanyId = company, Code = await NextCodeAsync(company, ct),
            CreatedBy = employee, CreatedDate = DateTime.Now
        };
        if (id.HasValue && request.EvaluationTemplateId.HasValue && request.EvaluationTemplateId != entity.EvaluationTemplateId)
            throw new EvaluationRequestException("Không đổi mẫu nguồn của phiếu đã lưu.", 409);
        await ApplySampleAsync(entity, request, company, ct);
        if (creator is not null)
        {
            entity.CreatedBy = creator.EmployeeId;
            entity.CreatedByEmployee = creator;
        }
        if (request.TestDate.HasValue) entity.TestDate = request.TestDate.Value;
        if (request.SupplierId.HasValue)
        {
            entity.SupplierId = request.SupplierId;
            entity.Supplier = supplier;
        }
        if (request.ClearFields?.Contains("supplierId") == true)
        {
            entity.SupplierId = null;
            entity.Supplier = null;
        }
        if (request.BatchNumber is not null)
            PatchHelper.Set(access.Normalize(request.BatchNumber), () => entity.BatchNumber, x => entity.BatchNumber = x);
        if (request.WeightKg.HasValue)
            PatchHelper.SetNullable(request.WeightKg, () => entity.WeightKg, x => entity.WeightKg = x);
        if (request.ClearFields?.Contains("batchNumber") == true) entity.BatchNumber = null;
        if (request.ClearFields?.Contains("weightKg") == true) entity.WeightKg = null;
        if (request.Note is not null) entity.Note = access.Normalize(request.Note);
        if (request.ClearFields?.Contains("note") == true) entity.Note = null;
        var template = !id.HasValue ? await SelectTemplateAsync(request.EvaluationTemplateId, company, ct) : null;
        if (!id.HasValue)
        {
            entity.EvaluationTemplateId = template?.Id;
            entity.TemplateCode = template?.Code;
            entity.TemplateName = template?.Name;
            entity.TemplateVersion = template?.Version;
        }
        var inputs = request.Results ?? (!id.HasValue && template is not null
            ? template.Items.OrderBy(x => x.SortOrder).Select(x => new LabTestResultInput(x.EvaluationStandardId)).ToList() : []);
        foreach (var input in inputs)
        {
            var row = entity.Results.SingleOrDefault(x => x.EvaluationStandardId == input.EvaluationStandardId);
            if (row is null)
            {
                var item = template?.Items.SingleOrDefault(x => x.EvaluationStandardId == input.EvaluationStandardId);
                var fromTemplate = item is not null;
                if (item is not null)
                {
                    if (!await db.EvaluationStandards.AnyAsync(x => x.Id == item.EvaluationStandardId && x.CompanyId == company, ct) ||
                        item.MeasurementMethodId.HasValue && !await db.MeasurementMethods.AnyAsync(x => x.Id == item.MeasurementMethodId && x.CompanyId == company, ct))
                        throw new EvaluationRequestException("Nguồn chỉ tiêu/phương pháp của mẫu không thuộc công ty.");
                }
                else
                {
                    var standard = await access.StandardAsync(input.EvaluationStandardId, company, ct);
                    var method = await access.MethodAsync(standard.MeasurementMethodId, company, ct);
                    item = EvaluationSnapshotFactory.CreateTemplateItem(standard, new EvaluationTemplate { CompanyId = company }, method);
                }
                EvaluationConfiguration.Merge(EvaluationConfiguration.Read(item), new(), null);
                row = BasicLabTestMapper.Snapshot(item, entity.Id, fromTemplate);
                row.SortOrder = entity.Results.Count + 1;
                if (entity.Results.Any(x => x.CriterionCode.Equals(row.CriterionCode, StringComparison.OrdinalIgnoreCase)))
                    throw new EvaluationRequestException("Trùng mã chỉ tiêu trên phiếu.", 409);
                entity.Results.Add(row);
                if (id.HasValue) db.LabTestResults.Add(row);
            }
            row.Value = EvaluationValueRules.Normalize(input.ClearValue ? null : input.Value ?? row.Value, BasicLabTestMapper.Configuration(row));
        }
        if (entity.Results.Count > 200) throw new EvaluationRequestException("Tối đa 200 chỉ tiêu mỗi phiếu.");
        if (entity.Results.Any(x => x.IsRequired && x.DataType != EvaluationDataType.Attachment && x.Value is null))
            throw new EvaluationRequestException("Vui lòng điền kết quả cho các chỉ tiêu bắt buộc.");
        if (id.HasValue) { entity.UpdatedBy = employee; entity.UpdatedDate = DateTime.Now; }
        else db.LabTests.Add(entity);
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return BasicLabTestMapper.ToDto(entity);
    }

    private async Task<LabTest> FindAsync(Guid id, Guid company, bool tracking, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new EvaluationRequestException("Id không hợp lệ.");
        var query = db.LabTests.Where(x => x.Id == id && x.CompanyId == company);
        if (!tracking) query = query.AsNoTracking();
        return await query.Include(x => x.Results).Include(x => x.Supplier).Include(x => x.CreatedByEmployee).FirstOrDefaultAsync(ct)
            ?? throw new EvaluationRequestException("Không tìm thấy mẫu test trong công ty hiện tại.", 404);
    }

    private async Task<EvaluationTemplate?> SelectTemplateAsync(Guid? id, Guid company, CancellationToken ct)
    {
        var now = DateTime.Now;
        var query = db.EvaluationTemplates.AsNoTracking().Include(x => x.Items).Where(x => x.CompanyId == company &&
            x.Type == EvaluationTemplateType.Iqc && x.Status == EvaluationTemplateStatus.Published &&
            (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= now));
        var template = id.HasValue ? await query.SingleOrDefaultAsync(x => x.Id == id, ct)
            : await query.OrderByDescending(x => x.Version).ThenByDescending(x => x.Id).FirstOrDefaultAsync(x => x.IsDefault, ct);
        if (id.HasValue && template is null) throw new EvaluationRequestException("Mẫu IQC chưa phát hành/có hiệu lực hoặc ngoài công ty.", 404);
        if (template is not null && (template.Items.Count > 200 || template.Items.Select(x => x.EvaluationStandardId).Distinct().Count() != template.Items.Count))
            throw new EvaluationRequestException("Mẫu vượt 200 chỉ tiêu hoặc chứa dòng trùng.");
        return template;
    }

    private static void Validate(BasicLabTestWriteRequest request, bool create)
    {
        if (request is null) throw new EvaluationRequestException("Dữ liệu không được null.");
        if ((create && !request.MaterialId.HasValue || request.SampleName is not null) &&
            (string.IsNullOrWhiteSpace(request.SampleName) || request.SampleName.Trim().Length > 200))
            throw new EvaluationRequestException("Tên mẫu bắt buộc, tối đa 200 ký tự.");
        if (create && !request.TestDate.HasValue || request.TestDate == DateOnly.MinValue || request.Note?.Length > 16000)
            throw new EvaluationRequestException("Ngày test bắt buộc; ghi chú tối đa 16000 ký tự.");
        if (request.BatchNumber?.Trim().Length > 100)
            throw new EvaluationRequestException("Số lot tối đa 100 ký tự.");
        if (request.WeightKg is { } weight && (weight < 0 || weight > 999999999999.999999m || decimal.Round(weight, 6) != weight))
            throw new EvaluationRequestException("Khối lượng không âm, tối đa 12 chữ số nguyên và 6 chữ số thập phân.");
        if (request.ClearFields?.Any(x => x is not ("note" or "batchNumber" or "weightKg" or "supplierId" or "materialId")) == true ||
            request.MaterialId.HasValue && request.ClearFields?.Contains("materialId") == true ||
            request.SupplierId.HasValue && request.ClearFields?.Contains("supplierId") == true ||
            request.Note is not null && request.ClearFields?.Contains("note") == true ||
            request.BatchNumber is not null && request.ClearFields?.Contains("batchNumber") == true ||
            request.WeightKg.HasValue && request.ClearFields?.Contains("weightKg") == true)
            throw new EvaluationRequestException("clearFields chỉ cho phép note, batchNumber, weightKg, supplierId, materialId và không đồng thời gửi giá trị.");
        if (request.Results is { } rows && (rows.Count > 200 || rows.Any(x => x is null || x.EvaluationStandardId == Guid.Empty ||
            x.ClearValue && x.Value is not null || x.ObservedText is not null || x.ClearObservedText) ||
            rows.Select(x => x.EvaluationStandardId).Distinct().Count() != rows.Count))
            throw new EvaluationRequestException("Kết quả không hợp lệ/trùng; không nhận dữ liệu import hoặc observedText.");
    }

    private async Task<string> NextCodeAsync(Guid company, CancellationToken ct)
    {
        var codes = await db.LabTests.Where(x => x.CompanyId == company && x.Code.StartsWith(CodePrefix)).Select(x => x.Code).ToListAsync(ct);
        var max = codes.Select(x => long.TryParse(x[CodePrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty().Max();
        if (max == long.MaxValue) throw new EvaluationRequestException("Đã hết dải mã mẫu test.", 409);
        return CodePrefix + (max + 1).ToString("D5", CultureInfo.InvariantCulture);
    }
}
