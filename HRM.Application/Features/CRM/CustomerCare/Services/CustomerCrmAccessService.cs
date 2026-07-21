using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Tập trung các rule truy cập CRM để command/query không tin trực tiếp CustomerId hoặc EmployeeId do FE gửi lên.
/// </summary>
internal sealed class CustomerCrmAccessService
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public CustomerCrmAccessService(
        ICRMReadDbContext readDbContext,
        ICustomerVisibilityService visibilityService)
    {
        _readDbContext = readDbContext;
        _visibilityService = visibilityService;
    }

    /// <summary>
    /// Xây dựng visibility scope của người dùng hiện tại theo company, role và phạm vi nhân viên.
    /// </summary>
    public Task<ViewerScope> BuildScopeAsync(CancellationToken cancellationToken)
        => _visibilityService.BuildScopeAsync(cancellationToken);

    /// <summary>
    /// Áp dụng company và customer visibility scope vào nguồn Customer read-only.
    /// </summary>
    public IQueryable<Customer> VisibleCustomers(ViewerScope scope)
        => _visibilityService.ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope);

    /// <summary>
    /// Kiểm tra nhân viên được yêu cầu có thuộc company và scope được phép hay không.
    /// Guid null biểu thị không lọc theo một nhân viên cụ thể.
    /// </summary>
    public async Task<EmployeeResolution> ResolveEmployeeAsync(
        ViewerScope scope,
        Guid? requestedEmployeeId,
        bool defaultToCurrent,
        CancellationToken cancellationToken)    
    {
        Guid? employeeId = requestedEmployeeId is { } requested && requested != Guid.Empty
            ? requested
            : defaultToCurrent ? scope.EmployeeId : null;

        if (!employeeId.HasValue)
        {
            return EmployeeResolution.Allowed(null);
        }

        if (!scope.HasFullCustomerView && !scope.EmployeeIdsInScope.Contains(employeeId.Value))
        {
            return EmployeeResolution.Denied();
        }

        var exists = await _readDbContext.Employees
            .AsNoTracking()
            .AnyAsync(x =>
                x.EmployeeId == employeeId.Value &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive,
                cancellationToken);

        return exists ? EmployeeResolution.Allowed(employeeId) : EmployeeResolution.Denied();
    }
}

/// <summary>
/// Kết quả resolve bộ lọc/người phụ trách nhằm phân biệt rõ không chọn nhân viên và chọn ngoài quyền.
/// </summary>
internal sealed record EmployeeResolution(bool IsAllowed, Guid? EmployeeId)
{
    public static EmployeeResolution Allowed(Guid? employeeId) => new(true, employeeId);
    public static EmployeeResolution Denied() => new(false, null);
}
