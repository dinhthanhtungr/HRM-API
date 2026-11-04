using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Patching;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.CreateMeasurementMethod;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.PatchMeasurementMethod;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Services;

/// <summary>Danh mục phương pháp đo: đọc theo company, tạo và PATCH với audit.</summary>
internal sealed class MeasurementMethodService(IEvaluationDbContext db,
    EvaluationAccessService access,
    KeyedMutationLock<string> mutationLock)
{
    /// <summary>Danh sách phương pháp đo theo company, có lọc và phân trang.</summary>
    public async Task<PagedResult<MeasurementMethodDto>> ListMethodsAsync(string? keyword, bool? active, int pageNumber, int pageSize, CancellationToken ct)
    {
        var company = access.Company();
        var query = db.MeasurementMethods.AsNoTracking().Where(x => x.CompanyId == company);
        if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
        if (access.Normalize(keyword) is { } term)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(term);
            query = query.Where(x => EF.Functions.ILike(x.Code, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name, pattern, PostgresSearchPattern.EscapeCharacter) ||
                x.NameEn != null && EF.Functions.ILike(x.NameEn, pattern, PostgresSearchPattern.EscapeCharacter));
        }
        var skip = access.PageOffset(pageNumber, pageSize);
        var total = await query.CountAsync(ct);
        var items = (await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Code).ThenBy(x => x.Id).Skip(skip).Take(pageSize).ToListAsync(ct)).Select(MethodDto).ToList();
        return new(items, total, pageNumber, pageSize);
    }

    /// <summary>Chi tiết phương pháp đo thuộc company hiện tại.</summary>
    public async Task<MeasurementMethodDto> GetMethodAsync(Guid id, CancellationToken ct)
    {
        var company = access.Company();
        var entity = await db.MeasurementMethods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == company, ct)
            ?? throw new EvaluationRequestException("Không tìm thấy phương pháp đo.", 404);
        return MethodDto(entity);
    }

    /// <summary>Tạo phương pháp đo, kiểm tra mã trùng và lưu audit.</summary>
    public async Task<MeasurementMethodDto> CreateMethodAsync(CreateMeasurementMethodCommand request, CancellationToken ct)
    {
        var company = access.Company(true);
        if (request.NameEn?.Trim().Length > 200)
            throw new EvaluationRequestException("NameEn tối đa 200 ký tự.");
        var employee = await access.EmployeeAsync(company, ct);
        access.ValidateName(request.Code, request.Name);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var code = request.Code.Trim();
        if (await db.MeasurementMethods.AnyAsync(x => x.CompanyId == company && x.Code.ToUpper() == code.ToUpper(), ct))
            throw new EvaluationRequestException("Mã phương pháp đo đã tồn tại.", 409);
        var entity = new MeasurementMethod
        {
            Id = Guid.CreateVersion7(), CompanyId = company, Code = code, Name = request.Name.Trim(), NameEn = access.Normalize(request.NameEn),
            Description = access.Normalize(request.Description), SortOrder = checked((await db.MeasurementMethods.Where(x => x.CompanyId == company)
                .OrderByDescending(x => x.SortOrder).Select(x => (int?)x.SortOrder).FirstOrDefaultAsync(ct) ?? 0) + 1),
            CreatedBy = employee, CreatedDate = DateTime.Now
        };
        db.MeasurementMethods.Add(entity);
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return MethodDto(entity);
    }

    /// <summary>PATCH phương pháp đo; chỉ ghi audit khi có thay đổi.</summary>
    public async Task<MeasurementMethodDto> PatchMethodAsync(PatchMeasurementMethodCommand request, CancellationToken ct)
    {
        var company = access.Company(true);
        if (request.NameEn?.Trim().Length > 200)
            throw new EvaluationRequestException("NameEn tối đa 200 ký tự.");
        var employee = await access.EmployeeAsync(company, ct);
        var clear = request.ClearFields ?? [];
        if (clear.Any(x => x != "description" && x != "nameEn") ||
            clear.Contains("description") && request.Description is not null ||
            clear.Contains("nameEn") && request.NameEn is not null)
            throw new EvaluationRequestException("clearFields chỉ cho phép description/nameEn, không gửi value cùng lúc.");
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var entity = await db.MeasurementMethods.FirstOrDefaultAsync(x => x.Id == request.Id && x.CompanyId == company, ct)
            ?? throw new EvaluationRequestException("Không tìm thấy phương pháp đo.", 404);
        var code = request.Code?.Trim() ?? entity.Code;
        var name = request.Name?.Trim() ?? entity.Name;
        access.ValidateName(code, name, entity.SortOrder);
        if (await db.MeasurementMethods.AnyAsync(x => x.CompanyId == company && x.Id != entity.Id && x.Code.ToUpper() == code.ToUpper(), ct))
            throw new EvaluationRequestException("Mã phương pháp đo đã tồn tại.", 409);
        var changed = PatchHelper.Set(code, () => entity.Code, x => entity.Code = x);
        changed |= PatchHelper.Set(name, () => entity.Name, x => entity.Name = x);
        changed |= PatchHelper.SetNullableRef(clear.Contains("nameEn") ? null : request.NameEn is null ? entity.NameEn : access.Normalize(request.NameEn),
            () => entity.NameEn, x => entity.NameEn = x);
        changed |= PatchHelper.SetNullableRef(clear.Contains("description") ? null : request.Description is null ? entity.Description : access.Normalize(request.Description), () => entity.Description, x => entity.Description = x);
        changed |= PatchHelper.SetIfHasValue(request.IsActive, () => entity.IsActive, x => entity.IsActive = x);
        if (changed)
        {
            entity.UpdatedBy = employee;
            entity.UpdatedDate = DateTime.Now;
            await access.SaveAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return MethodDto(entity);
    }
    private static MeasurementMethodDto MethodDto(MeasurementMethod x) => new(x.Id, x.Code, x.Name, x.Description, x.IsActive, x.SortOrder, x.CreatedDate, x.CreatedBy, x.UpdatedDate, x.UpdatedBy, x.NameEn);

}
