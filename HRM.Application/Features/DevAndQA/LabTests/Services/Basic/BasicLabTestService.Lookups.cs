using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTestLookup;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.Employees;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

internal sealed partial class BasicLabTestService
{
    private IQueryable<Employee> LabEmployees(Guid company) => db.Employees.AsNoTracking()
        .Where(employee => employee.CompanyId == company && employee.IsActive && employee.Status == EmployeeStatus.Active.ToString() &&
            db.Users.Any(user => user.EmployeeId == employee.EmployeeId &&
                (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow) &&
                db.UserRoles.Any(link => link.UserId == user.Id && link.IsActive &&
                    db.Roles.Any(role => role.Id == link.RoleId && role.NormalizedName == ApplicationRoles.Lab.LabUser.ToUpper()))));

    public async Task<PagedResult<LabTestLookupDto>> LookupAsync(GetLabTestLookupQuery request, CancellationToken ct)
    {
        var company = access.Company(false, EvaluationTemplateType.Iqc);
        var skip = access.PageOffset(request.PageNumber, request.PageSize);
        if (request.Keyword?.Length > 200 || !Enum.IsDefined(request.Kind))
            throw new EvaluationRequestException("Lookup không hợp lệ.");
        var query = BuildLookupQuery(request, company);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(skip).Take(request.PageSize).ToListAsync(ct);
        return new(items, total, request.PageNumber, request.PageSize);
    }

    internal IQueryable<LabTestLookupDto> BuildLookupQuery(GetLabTestLookupQuery request, Guid company)
    {
        IQueryable<LabTestLookupDto> query;
        if (request.Kind == LabTestLookupKind.Supplier)
        {
            var suppliers = db.Suppliers.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive == true);
            if (request.SelectedId.HasValue) suppliers = suppliers.Where(x => x.SupplierId == request.SelectedId);
            suppliers = suppliers.WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.SupplierName ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
            query = suppliers.Select(x => new LabTestLookupDto { Id = x.SupplierId, Code = x.ExternalId ?? "", Name = x.SupplierName ?? "" });
        }
        else if (request.Kind == LabTestLookupKind.Material)
        {
            var materials = db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive == true);
            if (request.SelectedId.HasValue) materials = materials.Where(x => x.MaterialId == request.SelectedId);
            materials = materials.WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
            query = materials.Select(x => new LabTestLookupDto { Id = x.MaterialId, Code = x.ExternalId ?? "", Name = x.Name ?? "" });
        }
        else
        {
            var employees = LabEmployees(company);
            if (request.SelectedId.HasValue) employees = employees.Where(x => x.EmployeeId == request.SelectedId);
            employees = employees.WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.FullName, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
            query = employees.Select(x => new LabTestLookupDto { Id = x.EmployeeId, Code = x.ExternalId, Name = x.FullName });
        }
        return query;
    }
}
