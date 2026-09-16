using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseVoucherById;

internal sealed class GetWarehouseVoucherByIdQueryHandler
    : IRequestHandler<GetWarehouseVoucherByIdQuery, OperationResult<WarehouseVoucherDetailResponseDto>>
{
    private readonly IWarehouseReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IWarehouseStockVisibilityService _stockVisibilityService;

    public GetWarehouseVoucherByIdQueryHandler(
        IWarehouseReadDbContext dbContext,
        ICurrentUser currentUser,
        IWarehouseStockVisibilityService stockVisibilityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _stockVisibilityService = stockVisibilityService;
    }

    public async Task<OperationResult<WarehouseVoucherDetailResponseDto>> Handle(
        GetWarehouseVoucherByIdQuery request, CancellationToken cancellationToken)
    {
        if (!WarehouseVoucherAccessRules.CanRead(_currentUser) || request.VoucherId <= 0)
        {
            return OperationResult<WarehouseVoucherDetailResponseDto>.Fail("Không tìm thấy phiếu kho.");
        }

        var companyId = _currentUser.CompanyId!.Value;
        var vouchers = _dbContext.WarehouseVouchers
            .AsNoTracking()
            .Where(x => x.VoucherId == request.VoucherId && x.CompanyId == companyId);
        var requests = _dbContext.WarehouseRequests
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);
        var header = await WarehouseVoucherDetailQueryHelper
            .BuildHeaders(vouchers, requests)
            .FirstOrDefaultAsync(cancellationToken);
        if (header is null)
        {
            return OperationResult<WarehouseVoucherDetailResponseDto>.Fail("Không tìm thấy phiếu kho.");
        }

        var voucherDetails = _dbContext.WarehouseVoucherDetails
            .AsNoTracking()
            .Where(x => x.VoucherId == header.VoucherId);
        var visibleDetails = await _stockVisibilityService.ApplyVoucherDetailsAsync(
            voucherDetails,
            companyId,
            request.Keyword,
            cancellationToken);
        var detailEntities = await visibleDetails
            .OrderBy(x => x.LineNo)
            .ToListAsync(cancellationToken);
        if (detailEntities.Count == 0 && !WarehouseVoucherAccessRules.CanReadAll(_currentUser))
        {
            return OperationResult<WarehouseVoucherDetailResponseDto>.Fail("Không tìm thấy phiếu kho.");
        }
        var voucherDetailIds = detailEntities.Select(x => x.VoucherDetailId).ToArray();
        var movementDates = await _dbContext.WarehouseShelfLedgers
            .AsNoTracking()
            .Where(x => x.VoucherDetailId != null && voucherDetailIds.Contains(x.VoucherDetailId.Value))
            .GroupBy(x => x.VoucherDetailId!.Value)
            .Select(x => new
            {
                VoucherDetailId = x.Key,
                MovementDate = x.Max(y => y.CreatedAt)
            })
            .ToDictionaryAsync(x => x.VoucherDetailId, x => x.MovementDate, cancellationToken);
        var companyName = await _dbContext.Companies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var createdByName = await _dbContext.Employees
            .AsNoTracking()
            .Where(x => x.EmployeeId == header.CreatedBy && x.CompanyId == companyId)
            .Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return OperationResult<WarehouseVoucherDetailResponseDto>.Ok(new WarehouseVoucherDetailResponseDto
        {
            VoucherId = header.VoucherId,
            VoucherCode = header.VoucherCode,
            VoucherType = header.VoucherType,
            Status = header.Status,
            CreatedDate = header.CreatedDate,
            RequestId = header.RequestId,
            RequestCode = header.RequestCode,
            RequestName = header.RequestName,
            ReqStatus = header.ReqStatus,
            ReqType = header.ReqType,
            CodeFromRequest = header.CodeFromRequest,
            CompanyId = header.CompanyId,
            CompanyName = companyName,
            CreatedBy = header.CreatedBy,
            CreatedByName = createdByName,
            Details = detailEntities
                .Select(x => WarehouseVoucherReadModelMapper.ToDetailDto(
                    x,
                    movementDates.GetValueOrDefault(x.VoucherDetailId)))
                .ToList()
        });
    }
}
