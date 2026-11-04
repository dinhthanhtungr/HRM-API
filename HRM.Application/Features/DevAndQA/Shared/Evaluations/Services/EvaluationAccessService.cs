using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;

/// <summary>Kiểm tra quyền/company/employee, lookup danh mục và validation dùng chung; không chứa luồng CRUD theo feature.</summary>
internal sealed class EvaluationAccessService(
    IEvaluationDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
{
    /// <summary>Ngoại lệ sửa nội dung non-Draft; không cấp quyền tạo/đổi mã hoặc thay kiểm tra capability/company.</summary>
    public bool CanEditNonDraftTemplate => user.IsAuthenticated &&
        user.IsInAnyRole(ApplicationRoleSets.DevAndQA.NonDraftTemplateEditors);

    public bool CanManageTemplateDefinition => user.IsAuthenticated &&
        user.IsInAnyRole(ApplicationRoleSets.DevAndQA.TemplateDefinitionManagers);

    public bool HasPermission(string permission) => permissions.HasPermission(permission);

    /// <summary>Ngoại lệ tạm thời: đồng bộ catalog dùng role QCUser/Developer/LabUser, không dùng permission claims.</summary>
    public Guid CatalogSyncCompany()
    {
        if (!user.IsAuthenticated || !user.IsInAnyRole(ApplicationRoleSets.DevAndQA.CatalogSyncManagers))
            throw new EvaluationRequestException("Chỉ QCUser, Developer hoặc LabUser được đồng bộ từ danh mục.", 403);
        if (user.CompanyId is not { } company || company == Guid.Empty)
            throw new EvaluationRequestException("Không lấy được công ty hiện tại.", 403);
        return company;
    }


    public Guid Company(bool write = false, EvaluationTemplateType? type = null)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty)
            throw new EvaluationRequestException("Không lấy được công ty hiện tại.", 403);
        var iqc = permissions.HasPermission(write ? ApplicationPermissions.DevAndQA.ManageIqc : ApplicationPermissions.DevAndQA.ViewIqc);
        var coa = permissions.HasPermission(write ? ApplicationPermissions.DevAndQA.ManageCoa : ApplicationPermissions.DevAndQA.ViewCoa);
        var allowed = type == EvaluationTemplateType.Coa ? coa : type.HasValue ? iqc : iqc || coa;
        if (!allowed)
            throw new EvaluationRequestException("Không có quyền thao tác dữ liệu đánh giá kỹ thuật.", 403);
        return company;
    }

    public async Task<Guid> EmployeeAsync(Guid company, CancellationToken ct)
    {
        if (user.EmployeeId is not { } employee || employee == Guid.Empty ||
            !await db.Employees.AnyAsync(x => x.EmployeeId == employee && x.CompanyId == company, ct))
            throw new EvaluationRequestException("Nhân viên không thuộc công ty hiện tại.", 403);
        return employee;
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
        {
            throw new EvaluationRequestException(
                "Dữ liệu xung đột hoặc liên kết không hợp lệ. Hãy tải lại.", 409, exception);
        }
    }

    public int PageOffset(int pageNumber, int pageSize)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 200 || ((long)pageNumber - 1) * pageSize > int.MaxValue)
            throw new EvaluationRequestException("Phân trang không hợp lệ; pageSize từ 1 đến 200.");
        return (pageNumber - 1) * pageSize;
    }

    public string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void ValidateName(string? code, string? name, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50 || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 || sortOrder < 0)
            throw new EvaluationRequestException("Code/Name bắt buộc, tối đa 50/200 ký tự; SortOrder không được âm.");
    }

    public async Task<MeasurementMethod?> MethodAsync(Guid? id, Guid company, CancellationToken ct)
    {
        if (id is null) return null;
        return await db.MeasurementMethods.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == company && x.IsActive, ct)
            ?? throw new EvaluationRequestException("Phương pháp đo không hợp lệ hoặc đã ngừng sử dụng.");
    }

    public async Task<EvaluationStandard> StandardAsync(Guid id, Guid company, CancellationToken ct) =>
        await db.EvaluationStandards.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == company && x.IsActive, ct)
        ?? throw new EvaluationRequestException("Chỉ tiêu không hợp lệ hoặc đã ngừng sử dụng.");
}
