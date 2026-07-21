using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Resolves blocking tax-code conflicts inside the current company using the legacy sales rule.
/// </summary>
internal sealed class CustomerTaxCodeConflictService
{
    private readonly ICRMReadDbContext _dbContext;

    public CustomerTaxCodeConflictService(ICRMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerTaxCodeConflict?> FindBlockingConflictAsync(
        Guid companyId,
        string? taxNumber,
        Guid? excludedCustomerId,
        CancellationToken cancellationToken)
    {
        var normalizedTaxCode = NormalizeTaxCode(taxNumber);
        if (normalizedTaxCode.Length == 0)
        {
            return null;
        }

        // TaxNumber has no normalized database column yet, so only IDs and tax codes are read
        // before applying the exact legacy normalization in memory.
        var taxRows = await _dbContext.Customers
            .AsNoTracking()
            .Where(customer =>
                customer.CompanyId == companyId &&
                customer.IsActive == true &&
                customer.TaxNumber != null &&
                (!excludedCustomerId.HasValue || customer.CustomerId != excludedCustomerId.Value))
            .Select(customer => new { customer.CustomerId, customer.TaxNumber })
            .ToListAsync(cancellationToken);

        var duplicateCustomerIds = taxRows
            .Where(row => NormalizeTaxCode(row.TaxNumber) == normalizedTaxCode)
            .Select(row => row.CustomerId)
            .ToArray();

        if (duplicateCustomerIds.Length == 0)
        {
            return null;
        }

        var conflict = await _dbContext.Customers
            .AsNoTracking()
            .Where(customer =>
                duplicateCustomerIds.Contains(customer.CustomerId) &&
                _dbContext.MerchandiseOrders.Any(order =>
                    order.CustomerId == customer.CustomerId &&
                    order.CompanyId == companyId &&
                    order.IsActive))
            .Select(customer => new
            {
                customer.CustomerId,
                customer.ExternalId,
                customer.CustomerName,
                customer.TaxNumber,
                Assignment = customer.CustomerAssignments
                    .Where(assignment => assignment.IsActive)
                    .OrderByDescending(assignment => assignment.CreatedDate)
                    .Select(assignment => new
                    {
                        EmployeeId = (Guid?)assignment.EmployeeId,
                        EmployeeName = assignment.Employee.FullName,
                        GroupId = (Guid?)assignment.GroupId,
                        GroupName = assignment.Group.Name
                    })
                    .FirstOrDefault(),
                LastOrderManager = _dbContext.MerchandiseOrders
                    .Where(order =>
                        order.CustomerId == customer.CustomerId &&
                        order.CompanyId == companyId &&
                        order.IsActive)
                    .OrderByDescending(order => order.CreateDate)
                    .Select(order => new
                    {
                        EmployeeId = (Guid?)order.ManagerById,
                        EmployeeName = order.ManagerByNameSnapshot
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (conflict is null)
        {
            return null;
        }

        return new CustomerTaxCodeConflict(
            conflict.CustomerId,
            conflict.ExternalId,
            conflict.CustomerName,
            conflict.TaxNumber,
            conflict.Assignment != null
                ? conflict.Assignment.EmployeeId
                : conflict.LastOrderManager != null ? conflict.LastOrderManager.EmployeeId : null,
            conflict.Assignment != null
                ? conflict.Assignment.EmployeeName
                : conflict.LastOrderManager != null ? conflict.LastOrderManager.EmployeeName : null,
            conflict.Assignment != null ? conflict.Assignment.GroupId : null,
            conflict.Assignment != null ? conflict.Assignment.GroupName : null);
    }

    public static string BuildConflictMessage(string? requestedTaxNumber, CustomerTaxCodeConflict conflict)
    {
        var employeeName = string.IsNullOrWhiteSpace(conflict.EmployeeName)
            ? "chua xac dinh"
            : conflict.EmployeeName;
        var groupName = string.IsNullOrWhiteSpace(conflict.GroupName)
            ? string.Empty
            : $" ({conflict.GroupName})";

        return $"Ma so thue {requestedTaxNumber?.Trim()} da thuoc khach hang {conflict.ExternalId} - " +
               $"\"{conflict.CustomerName}\" va khach hang nay da tung co don hang. " +
               $"Sale dang quan ly: {employeeName}{groupName}.";
    }

    private static string NormalizeTaxCode(string? tax)
    {
        if (string.IsNullOrWhiteSpace(tax)) return string.Empty;
        return new string(tax.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}

internal sealed record CustomerTaxCodeConflict(
    Guid CustomerId,
    string ExternalId,
    string CustomerName,
    string? TaxNumber,
    Guid? EmployeeId,
    string? EmployeeName,
    Guid? GroupId,
    string? GroupName);
