using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services;

/// <summary>
/// Đồng bộ vòng đời khách hàng khi lead phát sinh SaleOrder: cập nhật sale phụ trách,
/// assignment, claim và lịch sử chuyển đổi theo rule PLM.
/// </summary>
internal sealed class SaleOrderLeadConversionService
{
    private readonly ISaleOrderDbContext _dbContext;

    public SaleOrderLeadConversionService(ISaleOrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Khi lead phát sinh SaleOrder, sale tạo đơn trở thành người phụ trách chính.
    /// Khách nội bộ `KH_VIETAUS` vẫn giữ trạng thái lead theo rule PLM dùng chung.
    /// </summary>
    public async Task ApplyAsync(
        MerchandiseOrder order,
        Guid employeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(x =>
                x.CustomerId == order.CustomerId &&
                x.CompanyId == order.CompanyId,
                cancellationToken);
        if (customer is not { IsLead: true })
        {
            return;
        }

        var groupId = await _dbContext.MemberInGroups
            .AsNoTracking()
            .Where(x => x.Profile == employeeId && x.IsActive && x.Group.CompanyId == order.CompanyId)
            .OrderByDescending(x => x.IsAdmin == true)
            .Select(x => (Guid?)x.GroupId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!groupId.HasValue)
        {
            throw new InvalidOperationException("Không tìm thấy nhóm của nhân viên tạo đơn, không thể gán khách hàng.");
        }

        customer.IsLead = PLMCustomerRules.IsInternalCustomer(customer);
        customer.LeadStatus = LeadStatus.Open;
        customer.CurrentSaleId = employeeId;
        customer.UpdatedBy = employeeId;
        customer.UpdatedDate = now;

        await EnsureAssignmentAsync(order, employeeId, groupId.Value, now, cancellationToken);
        await CloseOtherActiveClaimsAsync(order, employeeId, now, cancellationToken);

        if (!PLMCustomerRules.IsInternalCustomer(customer))
        {
            await AddTransferLogAsync(order, employeeId, groupId.Value, now, cancellationToken);
        }
    }

    /// <summary>
    /// Tạo assignment cho sale lập đơn khi khách hàng chưa có assignment đang hoạt động.
    /// </summary>
    private async Task EnsureAssignmentAsync(
        MerchandiseOrder order,
        Guid employeeId,
        Guid groupId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hasActiveAssignment = await _dbContext.CustomerAssignments.AnyAsync(x =>
            x.CustomerId == order.CustomerId &&
            x.CompanyId == order.CompanyId &&
            x.IsActive,
            cancellationToken);
        if (hasActiveAssignment)
        {
            return;
        }

        await _dbContext.CustomerAssignments.AddAsync(new CustomerAssignment
        {
            Id = Guid.CreateVersion7(),
            CustomerId = order.CustomerId,
            EmployeeId = employeeId,
            GroupId = groupId,
            CompanyId = order.CompanyId,
            IsActive = true,
            CreatedDate = now,
            CreatedBy = employeeId,
            UpdatedDate = now,
            UpdatedBy = employeeId
        }, cancellationToken);
    }

    /// <summary>
    /// Đóng các claim nhận khách đang hoạt động của những sale khác sau khi khách phát sinh đơn.
    /// </summary>
    private async Task CloseOtherActiveClaimsAsync(
        MerchandiseOrder order,
        Guid employeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var claims = await _dbContext.CustomerClaims
            .Where(x =>
                x.CustomerId == order.CustomerId &&
                x.CompanyId == order.CompanyId &&
                x.Type == ClaimType.Work &&
                x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var claim in claims.Where(x => x.EmployeeId != employeeId))
        {
            claim.IsActive = false;
            claim.ExpiresAt = now;
        }
    }

    /// <summary>
    /// Ghi lịch sử chuyển khách từ Lead sang Customer cho mục đích audit CRM.
    /// </summary>
    private async Task AddTransferLogAsync(
        MerchandiseOrder order,
        Guid employeeId,
        Guid groupId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var logId = Guid.CreateVersion7();
        await _dbContext.CustomerTransferLogs.AddAsync(new CustomerTransferLog
        {
            Id = logId,
            FromEmployeeId = employeeId,
            ToEmployeeId = employeeId,
            FromGroupId = groupId,
            ToGroupId = groupId,
            TransferType = TransferType.Saled,
            Note = $"Khách hàng chuyển từ Lead sang Customer khi tạo đơn {order.ExternalId}",
            CreatedDate = now,
            CreatedBy = employeeId,
            CompanyId = order.CompanyId,
            DetailCustomerTransfers = new List<DetailCustomerTransfer>
            {
                new() { LogId = logId, CustomerId = order.CustomerId }
            }
        }, cancellationToken);
    }
}
