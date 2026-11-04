using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Patching;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CreateCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificate;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Services;

/// <summary>Phiếu COA: đọc, tạo nháp, PATCH thông tin, phát hành và hủy.</summary>
internal sealed partial class CoaCertificateService(IEvaluationDbContext db,
    EvaluationAccessService access,
    KeyedMutationLock<string> mutationLock,
    EvaluationTemplateService templates)
{
    private async Task<CoaCertificate> LoadCoaAsync(Guid id, Guid company, CancellationToken ct)
    {
        var certificate = await db.CoaCertificates.Include(x => x.Results).ThenInclude(x => x.CoaSpecificationItem).ThenInclude(x => x!.CoaSpecification)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == company, ct)
            ?? throw new EvaluationRequestException("Không tìm thấy phiếu COA.", 404);
        ValidateLegacyCoa(certificate, company);
        return certificate;
    }

    internal static void ValidateLegacyCoa(CoaCertificate certificate, Guid company)
    {
        if (certificate.Results.Where(x => !x.HasConfigurationSnapshot).Any(x =>
            x.CoaSpecificationItem is not { } item || item.CoaSpecificationId != certificate.CoaSpecificationId ||
            item.CoaSpecification is null || item.CoaSpecification.CompanyId != company))
            throw new EvaluationRequestException("Nguồn tiêu chuẩn cũ không hợp lệ; cần kiểm tra dữ liệu trước.");
    }

    private static void RequireDraft(CoaCertificate entity)
    {
        if (entity.Status != CoaCertificateStatus.Draft) throw new EvaluationRequestException("Chỉ sửa phiếu COA nháp.", 409);
    }

    private static void ValidateCoaHeader(string? number, string? batch, DateOnly? manufacturing, DateOnly? expiry)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 100 || string.IsNullOrWhiteSpace(batch) || batch.Trim().Length > 100)
            throw new EvaluationRequestException("CertificateNo/BatchNo bắt buộc, tối đa 100 ký tự.");
        if (manufacturing.HasValue && expiry.HasValue && expiry < manufacturing)
            throw new EvaluationRequestException("Ngày hết hạn phải từ ngày sản xuất trở đi.");
    }

    /// <summary>Danh sách COA trong company, trả snapshot đã lưu.</summary>
    public async Task<PagedResult<CoaCertificateDto>> ListCoaAsync(string? keyword, int pageNumber, int pageSize, CancellationToken ct)
    {
        var company = access.Company(false, EvaluationTemplateType.Coa);
        var query = db.CoaCertificates.AsNoTracking().Include(x => x.Results).ThenInclude(x => x.CoaSpecificationItem).ThenInclude(x => x!.CoaSpecification).Where(x => x.CompanyId == company);
        if (access.Normalize(keyword) is { } term)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(term);
            query = query.Where(x => EF.Functions.ILike(x.CertificateNo, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.ProductCode, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.BatchNo, pattern, PostgresSearchPattern.EscapeCharacter));
        }
        var skip = access.PageOffset(pageNumber, pageSize);
        var total = await query.CountAsync(ct);
        var entities = await query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id).Skip(skip).Take(pageSize).ToListAsync(ct);
        foreach (var entity in entities) ValidateLegacyCoa(entity, company);
        var items = entities.Select(CoaDto).ToList();
        return new(items, total, pageNumber, pageSize);
    }

    /// <summary>Chi tiết COA và các dòng kết quả, kiểm tra nguồn legacy.</summary>
    public async Task<CoaCertificateDto> GetCoaAsync(Guid id, CancellationToken ct) => CoaDto(await LoadCoaAsync(id, access.Company(false, EvaluationTemplateType.Coa), ct));

    /// <summary>Tạo COA Draft từ sản phẩm và template cùng company.</summary>
    public async Task<CoaCertificateDto> CreateCoaAsync(CreateCoaCertificateCommand request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Coa);
        var employee = await access.EmployeeAsync(company, ct);
        ValidateCoaHeader(request.CertificateNo, request.BatchNo, request.ManufacturingDate, request.ExpiryDate);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var number = request.CertificateNo.Trim();
        if (await db.CoaCertificates.AnyAsync(x => x.CompanyId == company && x.CertificateNo.ToUpper() == number.ToUpper(), ct))
            throw new EvaluationRequestException("Số COA đã tồn tại.", 409);
        var product = await db.Products.AsNoTracking().Where(x => x.ProductId == request.ProductId && x.CompanyId == company && x.IsActive)
            .Select(x => new { x.Code, x.Name }).FirstOrDefaultAsync(ct)
            ?? throw new EvaluationRequestException("Sản phẩm không hợp lệ hoặc ngoài công ty.");
        if (string.IsNullOrWhiteSpace(product.Code) || product.Code.Length > 100 || string.IsNullOrWhiteSpace(product.Name) || product.Name.Length > 200)
            throw new EvaluationRequestException("Sản phẩm thiếu mã/tên hoặc vượt giới hạn phiếu COA.");
        var template = await templates.SelectTemplateAsync(request.EvaluationTemplateId, EvaluationTemplateType.Coa, company, ct);
        var certificate = new CoaCertificate
        {
            Id = Guid.CreateVersion7(), CompanyId = company, CertificateNo = number,
            ProductCode = product.Code, ProductName = product.Name, BatchNo = request.BatchNo.Trim(),
            ManufacturingDate = request.ManufacturingDate, ExpiryDate = request.ExpiryDate, Notes = access.Normalize(request.Notes),
            CreatedBy = employee, CreatedDate = DateTime.Now
        };
        EvaluationSnapshotFactory.InitializeCoa(certificate, template);
        db.CoaCertificates.Add(certificate);
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return CoaDto(certificate);
    }

    /// <summary>PATCH đầu phiếu Draft; giữ field không gửi, kiểm tra clearFields.</summary>
    public async Task<CoaCertificateDto> PatchCoaAsync(PatchCoaCertificateCommand request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Coa);
        await access.EmployeeAsync(company, ct);
        var clear = request.ClearFields ?? [];
        if (clear.Any(x => x is not ("notes" or "manufacturingDate" or "expiryDate")) ||
            clear.Contains("notes") && request.Notes is not null ||
            clear.Contains("manufacturingDate") && request.ManufacturingDate.HasValue ||
            clear.Contains("expiryDate") && request.ExpiryDate.HasValue)
            throw new EvaluationRequestException("clearFields không hợp lệ hoặc xung đột value.");
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = await LoadCoaAsync(request.Id, company, ct);
        RequireDraft(entity);
        var number = request.CertificateNo?.Trim() ?? entity.CertificateNo;
        var batch = request.BatchNo?.Trim() ?? entity.BatchNo;
        var manufacturing = clear.Contains("manufacturingDate") ? null : request.ManufacturingDate ?? entity.ManufacturingDate;
        var expiry = clear.Contains("expiryDate") ? null : request.ExpiryDate ?? entity.ExpiryDate;
        ValidateCoaHeader(number, batch, manufacturing, expiry);
        if (await db.CoaCertificates.AnyAsync(x => x.CompanyId == company && x.Id != entity.Id && x.CertificateNo.ToUpper() == number.ToUpper(), ct))
            throw new EvaluationRequestException("Số COA đã tồn tại.", 409);
        var changed = PatchHelper.Set(number, () => entity.CertificateNo, x => entity.CertificateNo = x);
        changed |= PatchHelper.Set(batch, () => entity.BatchNo, x => entity.BatchNo = x);
        changed |= PatchHelper.Set(manufacturing, () => entity.ManufacturingDate, x => entity.ManufacturingDate = x);
        changed |= PatchHelper.Set(expiry, () => entity.ExpiryDate, x => entity.ExpiryDate = x);
        changed |= PatchHelper.SetNullableRef(clear.Contains("notes") ? null : request.Notes is null ? entity.Notes : access.Normalize(request.Notes), () => entity.Notes, x => entity.Notes = x);
        if (changed) await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return CoaDto(entity);
    }

    /// <summary>Phát hành khi dữ liệu hợp lệ và đủ dòng bắt buộc, không tự kết luận đạt.</summary>
    public async Task<CoaCertificateDto> IssueCoaAsync(Guid id, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Coa);
        var employee = await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = await LoadCoaAsync(id, company, ct);
        RequireDraft(entity);
        if (entity.Results.Count == 0) throw new EvaluationRequestException("COA chưa có chỉ tiêu.");
        foreach (var row in entity.Results)
        {
            var dto = ResultDto(row);
            EvaluationConfiguration.Merge(dto.Configuration, new(), null);
            if (dto.Configuration.DataType == EvaluationDataType.Attachment)
            {
                if (row.Value is not null) throw new EvaluationRequestException("Chỉ tiêu Attachment không nhận Value.");
                if (dto.Configuration.IsRequired && (row.AttachmentCollectionId is null ||
                    !await db.AttachmentModels.AnyAsync(x => x.AttachmentCollectionId == row.AttachmentCollectionId && x.IsActive &&
                        x.Slot == HRM.Domain.Enums.Attachment.AttachmentSlot.EvaluationAttachment, ct)))
                    throw new EvaluationRequestException($"Chưa đính kèm file cho {dto.CriterionName}.");
                continue;
            }
            if (dto.Configuration.IsRequired && row.Value is null) throw new EvaluationRequestException($"Chưa nhập {dto.CriterionName}.");
            EvaluationValueRules.Normalize(row.Value, dto.Configuration);
        }
        entity.Status = CoaCertificateStatus.Issued;
        entity.IssuedBy = employee;
        entity.IssuedDate = DateTime.Now;
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return CoaDto(entity);
    }

    /// <summary>Hủy phiếu và giữ dữ liệu; gọi lặp không ghi thêm.</summary>
    public async Task<CoaCertificateDto> CancelCoaAsync(Guid id, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Coa);
        await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = await LoadCoaAsync(id, company, ct);
        if (entity.Status != CoaCertificateStatus.Cancelled)
        {
            entity.Status = CoaCertificateStatus.Cancelled;
            await access.SaveAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return CoaDto(entity);
    }
    internal static EvaluationResultDto ResultDto(CoaCertificateResult x)
    {
        var config = EvaluationConfiguration.Read(x);
        var code = x.CriterionCode;
        var name = x.CriterionName;
        var nameEn = x.CriterionNameEn;
        var standardId = x.EvaluationStandardId;
        if (!x.HasConfigurationSnapshot && x.CoaSpecificationItem is { } old)
        {
            config = new(old.DataType, old.Unit, old.MeasurementMethodId, old.MeasurementMethodCode, old.TestMethod,
                old.InputConfigJson, old.AcceptanceRuleJson, old.SpecificationText, old.IsRequired,
                old.SortOrder);
            code = old.CriterionCode;
            name = old.CriterionName;
            nameEn = old.CriterionNameEn;
            standardId = old.EvaluationStandardId;
        }
        return new(x.Id, standardId, x.SourceTemplateItemId, x.ExternalId, code, name, x.HasConfigurationSnapshot, x.Value, config, nameEn, x.AttachmentCollectionId);
    }
    private static CoaCertificateDto CoaDto(CoaCertificate x) => new(x.Id, x.CertificateNo, x.ProductCode, x.ProductName, x.BatchNo, x.ManufacturingDate, x.ExpiryDate,
        x.Status, x.Notes, x.EvaluationTemplateId, x.TemplateCode, x.TemplateName, x.TemplateVersion, x.CreatedBy, x.CreatedDate, x.IssuedBy, x.IssuedDate,
        x.Results.OrderBy(i => i.SortOrder).Select(ResultDto).ToList());

}
