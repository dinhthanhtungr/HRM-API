using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Rules;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Chặn MST khách chính thức cùng công ty và khách tiềm năng cùng nhóm chăm sóc hiệu lực.
/// </summary>
internal sealed class CustomerTaxCodeConflictService
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly IDateTimeProvider _clock;

    public CustomerTaxCodeConflictService(ICRMReadDbContext dbContext, IDateTimeProvider clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<CustomerTaxCodeConflict?> FindBlockingConflictAsync(
        Guid companyId,
        string? taxNumber,
        Guid? excludedCustomerId,
        CancellationToken cancellationToken,
        Guid? creatingGroupId = null)
    {
        var normalizedTaxCode = NormalizeTaxCode(taxNumber);
        if (normalizedTaxCode.Length == 0)
        {
            return null;
        }

        var now = _clock.Now;
        var groupIds = new HashSet<Guid>();
        if (creatingGroupId is { } group && group != Guid.Empty)
            groupIds.Add(group);
        else if (excludedCustomerId.HasValue)
        {
            // Khi sửa, nhóm của bản ghi là nguồn quyết định; không dùng nhóm của người sửa.
            var target = await _dbContext.Customers.AsNoTracking()
                .Where(x => x.CustomerId == excludedCustomerId.Value && x.CompanyId == companyId && x.IsLead)
                .Select(x => new
                {
                    Assignments = x.CustomerAssignments.Where(a => a.IsActive && a.CompanyId == companyId)
                        .Select(a => a.GroupId).ToList(),
                    Claims = x.CustomerClaims.Where(c => c.IsActive && c.CompanyId == companyId
                        && c.Type == ClaimType.Work && c.ExpiresAt > now).Select(c => c.GroupId).ToList()
                }).FirstOrDefaultAsync(cancellationToken);
            if (target is not null)
            {
                groupIds.UnionWith(target.Assignments);
                groupIds.UnionWith(target.Claims);
            }
        }
        var targetGroupIds = groupIds.ToArray();

        // TaxNumber has no normalized database column yet, so the minimal customer fields are read
        // before applying the business normalization in memory.
        var taxRows = await _dbContext.Customers
            .AsNoTracking()
            .Where(customer =>
                customer.CompanyId == companyId &&
                customer.IsActive == true &&
                customer.TaxNumber != null &&
                (!excludedCustomerId.HasValue || customer.CustomerId != excludedCustomerId.Value))
            .Select(customer => new
            {
                customer.CustomerId,
                customer.ExternalId,
                customer.TaxNumber,
                customer.IsLead,
                SameLeadGroup = customer.CustomerAssignments.Any(a => a.IsActive && a.CompanyId == companyId
                    && targetGroupIds.Contains(a.GroupId)) || customer.CustomerClaims.Any(c => c.IsActive
                    && c.CompanyId == companyId && c.Type == ClaimType.Work && c.ExpiresAt > now
                    && targetGroupIds.Contains(c.GroupId))
            })
            .ToListAsync(cancellationToken);

        var duplicateCustomerIds = taxRows
            .Where(row =>
                (!row.IsLead || row.SameLeadGroup) &&
                !InternalCustomerRules.IsInternalCustomerExternalId(row.ExternalId) &&
                NormalizeTaxCode(row.TaxNumber) == normalizedTaxCode)
            .Select(row => row.CustomerId)
            .ToArray();

        if (duplicateCustomerIds.Length == 0)
        {
            return null;
        }

        var conflict = await _dbContext.Customers
            .AsNoTracking()
            .Where(customer => duplicateCustomerIds.Contains(customer.CustomerId))
            .OrderBy(customer => customer.IsLead)
            .Select(customer => new
            {
                customer.CustomerId,
                customer.ExternalId,
                customer.CustomerName,
                customer.TaxNumber,
                customer.IsLead,
                Assignment = customer.CustomerAssignments
                    .Where(assignment => assignment.IsActive && assignment.CompanyId == companyId
                        && (!customer.IsLead || targetGroupIds.Contains(assignment.GroupId)))
                    .OrderByDescending(assignment => assignment.CreatedDate)
                    .Select(assignment => new
                    {
                        EmployeeId = (Guid?)assignment.EmployeeId,
                        EmployeeName = assignment.Employee.FullName,
                        GroupId = (Guid?)assignment.GroupId,
                        GroupName = assignment.Group.Name
                    })
                    .FirstOrDefault(),
                Claim = customer.CustomerClaims
                    .Where(c => c.IsActive && c.CompanyId == companyId && c.Type == ClaimType.Work
                        && c.ExpiresAt > now && targetGroupIds.Contains(c.GroupId))
                    .OrderByDescending(c => c.ExpiresAt)
                    .Select(c => new { EmployeeId = (Guid?)c.EmployeeId, EmployeeName = c.Employee.FullName,
                        GroupId = (Guid?)c.GroupId, GroupName = c.Group.Name }).FirstOrDefault()
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
            conflict.Assignment?.EmployeeId ?? conflict.Claim?.EmployeeId,
            conflict.Assignment?.EmployeeName ?? conflict.Claim?.EmployeeName,
            conflict.Assignment?.GroupId ?? conflict.Claim?.GroupId,
            conflict.Assignment?.GroupName ?? conflict.Claim?.GroupName,
            conflict.IsLead);
    }

    public static string BuildConflictMessage(string? requestedTaxNumber, CustomerTaxCodeConflict conflict)
    {
        if (conflict.IsLead)
        {
            var leadSaleName = string.IsNullOrWhiteSpace(conflict.EmployeeName)
                ? "chưa xác định" : conflict.EmployeeName;
            var leadTeamName = string.IsNullOrWhiteSpace(conflict.GroupName)
                ? "chưa xác định" : conflict.GroupName;
            return $"Mã số thuế {requestedTaxNumber?.Trim()} đã thuộc khách hàng tiềm năng {conflict.ExternalId} - " +
                $"\"{conflict.CustomerName}\" trong cùng nhóm chăm sóc. " +
                $"Sale phụ trách: {leadSaleName}. Team đang giữ: {leadTeamName}. " +
                "Không được trùng MST trong cùng nhóm.";
        }
        var employeeName = string.IsNullOrWhiteSpace(conflict.EmployeeName)
            ? "chua xac dinh"
            : conflict.EmployeeName;
        var groupName = string.IsNullOrWhiteSpace(conflict.GroupName)
            ? string.Empty
            : $" ({conflict.GroupName})";

        return $"Ma so thue {requestedTaxNumber?.Trim()} da thuoc khach hang {conflict.ExternalId} - " +
               $"\"{conflict.CustomerName}\" va khach hang nay da la khach hang chinh thuc. " +
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
    string? GroupName,
    bool IsLead = false);
