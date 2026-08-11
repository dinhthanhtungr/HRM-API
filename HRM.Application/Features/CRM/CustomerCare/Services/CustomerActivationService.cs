using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Thay đổi trạng thái hoạt động của khách hàng trong đúng company/visibility scope và chỉ dành cho CustomerEditors.
/// </summary>
internal sealed class CustomerActivationService
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CustomerActivationService(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<CustomerActivationResultDto>> SetActiveAsync(
        Guid customerId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            return OperationResult<CustomerActivationResultDto>.Fail("CustomerId is invalid.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.CRM.CustomerEditors))
        {
            return OperationResult<CustomerActivationResultDto>.Fail(
                "You are not allowed to change customer activation status.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var canAccess = await _visibilityService
            .ApplyCustomerVisibilityIncludingInactive(_readDbContext.Customers.AsNoTracking(), scope)
            .AnyAsync(customer => customer.CustomerId == customerId, cancellationToken);
        if (!canAccess)
        {
            return OperationResult<CustomerActivationResultDto>.Fail(
                "Customer was not found or is outside your visibility scope.");
        }

        var customer = await _writeDbContext.Customers.FirstOrDefaultAsync(
            item => item.CustomerId == customerId && item.CompanyId == scope.CompanyId,
            cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerActivationResultDto>.Fail("Customer was not found.");
        }

        if (customer.IsActive != isActive)
        {
            customer.IsActive = isActive;
            customer.UpdatedBy = scope.EmployeeId;
            customer.UpdatedDate = _dateTimeProvider.Now;
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<CustomerActivationResultDto>.Ok(
            new CustomerActivationResultDto
            {
                CustomerId = customer.CustomerId,
                IsActive = customer.IsActive == true
            },
            isActive ? "Customer reactivated successfully." : "Customer deactivated successfully.");
    }
}
