using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandardDetail;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandards;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;

/// <summary>Quản lý danh mục đánh giá IQC: company scope, capability, mã không trùng và audit phía server.</summary>
internal sealed partial class EvaluationStandardService(
    IIqcDbContext context,
    ICurrentUser currentUser,
    ICurrentUserPermissionService permissions,
    KeyedMutationLock<string> mutationLock)
{
    private string? Access(bool mutation, out Guid companyId)
    {
        companyId = IqcRules.Company(currentUser) ?? Guid.Empty;
        if (companyId == Guid.Empty)
        {
            return "Không lấy được công ty hiện tại.";
        }

        return (permissions.HasPermission(mutation ? ApplicationPermissions.DevAndQA.ManageIqc : ApplicationPermissions.DevAndQA.ViewIqc) ||
            permissions.HasPermission(mutation ? ApplicationPermissions.DevAndQA.ManageCoa : ApplicationPermissions.DevAndQA.ViewCoa))
            ? null : "Không có quyền thao tác danh mục tiêu chuẩn IQC.";
    }

    public async Task<OperationResult<PagedResult<EvaluationStandardDto>>> ListAsync(
        GetEvaluationStandardsQuery request,
        CancellationToken ct)
    {
        var error = Access(false, out var companyId);

        if (error is not null)
        {
            return OperationResult<PagedResult<EvaluationStandardDto>>.Fail(error);
        }

        if (((long)request.NormalizedPageNumber - 1) * request.NormalizedPageSize > int.MaxValue)
        {
            return OperationResult<PagedResult<EvaluationStandardDto>>.Fail("Phân trang vượt giới hạn.");
        }

        var query = context.EvaluationStandards
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);
        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
            query = query.Where(x => EF.Functions.ILike(x.Code, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name, pattern, PostgresSearchPattern.EscapeCharacter) ||
                x.NameEn != null && EF.Functions.ILike(x.NameEn, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new EvaluationStandardDto(x.Id, x.Code, x.Name, x.Description, x.IsActive, x.SortOrder,
                x.CreatedDate, x.CreatedBy, x.UpdatedDate, x.UpdatedBy,
                x.DataType, x.Unit, x.TestMethod, x.InputConfigJson, x.AcceptanceRuleJson,
                x.SpecificationText, x.IsRequired, x.MeasurementMethodId, x.NameEn))
            .ToListAsync(ct);
        return OperationResult<PagedResult<EvaluationStandardDto>>.Ok(new PagedResult<EvaluationStandardDto>(rows, total, request.NormalizedPageNumber, request.NormalizedPageSize));
    }

    public async Task<OperationResult<EvaluationStandardDto?>> DetailAsync(
        GetEvaluationStandardDetailQuery request,
        CancellationToken ct)
    {
        var error = Access(false, out var companyId);

        if (error is not null)
        {
            return OperationResult<EvaluationStandardDto?>.Fail(error);
        }

        if (request.Id == Guid.Empty)
        {
            return OperationResult<EvaluationStandardDto?>.Fail("Id không hợp lệ.");
        }


        var entity = await context.EvaluationStandards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.CompanyId == companyId, ct);
        return OperationResult<EvaluationStandardDto?>.Ok(entity is null ? null : ToDto(entity));
    }

    private Task<bool> HasCodeAsync(
        string code,
        Guid companyId,
        Guid? exceptId,
        CancellationToken ct) =>
        context.EvaluationStandards.AnyAsync(x => x.CompanyId == companyId && x.Id != exceptId && x.Code.ToUpper() == code.ToUpper(), ct);

    private async Task<Guid?> EmployeeAsync(Guid companyId, CancellationToken ct) =>
        currentUser.EmployeeId is { } id && id != Guid.Empty &&
        await context.Employees.AnyAsync(x => x.EmployeeId == id && x.CompanyId == companyId, ct) ? id : null;

    private static string? Validate(string? code, string? name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50)
        {
            return "Code bắt buộc, tối đa 50 ký tự.";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return "Name bắt buộc, tối đa 200 ký tự.";
        }

        return sortOrder < 0 ? "SortOrder không được âm." : null;
    }

    private static string? Resolve(string? incoming, string? current, string field, IReadOnlyList<string> clearFields) =>
        clearFields.Contains(field) ? null : incoming is null ? current : Normalize(incoming);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static EvaluationStandardDto ToDto(EvaluationStandard x) => new(
        x.Id,
        x.Code,
        x.Name,
        x.Description,
        x.IsActive,
        x.SortOrder,
        x.CreatedDate,
        x.CreatedBy,
        x.UpdatedDate,
        x.UpdatedBy,
        x.DataType,
        x.Unit,
        x.TestMethod,
        x.InputConfigJson,
        x.AcceptanceRuleJson,
        x.SpecificationText,
        x.IsRequired,
        x.MeasurementMethodId, x.NameEn);
}
