using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Services;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseVouchers;

internal sealed class GetWarehouseVouchersQueryHandler
    : IRequestHandler<GetWarehouseVouchersQuery, OperationResult<PagedResult<WarehouseVoucherListItemDto>>>
{
    private readonly IWarehouseReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IWarehouseStockVisibilityService _stockVisibilityService;
    private readonly IWarehouseVoucherBusinessReader _businessReader;

    public GetWarehouseVouchersQueryHandler(
        IWarehouseReadDbContext dbContext,
        ICurrentUser currentUser,
        IWarehouseStockVisibilityService stockVisibilityService,
        IWarehouseVoucherBusinessReader businessReader)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _stockVisibilityService = stockVisibilityService;
        _businessReader = businessReader;
    }

    public async Task<OperationResult<PagedResult<WarehouseVoucherListItemDto>>> Handle(
        GetWarehouseVouchersQuery request, CancellationToken cancellationToken)
    {
        if (!WarehouseVoucherAccessRules.CanRead(_currentUser))
        {
            return OperationResult<PagedResult<WarehouseVoucherListItemDto>>.Fail(
                "Bạn không có quyền xem lịch sử phiếu kho.");
        }

        if (!WarehouseVoucherStatusRules.TryNormalizeFilter(request.Status, out var normalizedStatus))
        {
            return OperationResult<PagedResult<WarehouseVoucherListItemDto>>.Fail("Status không hợp lệ.");
        }

        if (request.FromDate > request.ToDate)
        {
            return OperationResult<PagedResult<WarehouseVoucherListItemDto>>.Fail("FromDate không được sau ToDate.");
        }

        if (request.ReqType.HasValue && !Enum.IsDefined(request.ReqType.Value))
        {
            return OperationResult<PagedResult<WarehouseVoucherListItemDto>>.Fail("ReqType không hợp lệ.");
        }

        var isIms = HRM.Application.Commons.Authorization.ImsAccountRestrictions.AppliesTo(_currentUser);
        var companyId = _currentUser.CompanyId!.Value;
        var vouchers = _dbContext.WarehouseVouchers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.RequestId != null);
        if (isIms) vouchers = ImsWarehouseVoucherScope.Apply(_dbContext, vouchers, companyId);
        var requests = _dbContext.WarehouseRequests
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);
        var headers = WarehouseVoucherListQueryHelper.BuildHeaders(vouchers, requests);

        var sampleProductColourCodes =
            from sampleRequest in _dbContext.SampleRequests.AsNoTracking()
            join product in _dbContext.Products.AsNoTracking()
                on sampleRequest.ProductId equals product.ProductId
            where sampleRequest.CompanyId == companyId
                  && sampleRequest.IsActive
                  && product.CompanyId == companyId
                  && product.ColourCode != null
                  && product.ColourCode != string.Empty
                  && request.NormalizedKeyword != null
                  && EF.Functions.ILike(
                      sampleRequest.ExternalId,
                      PostgresSearchPattern.ContainsLiteral(request.NormalizedKeyword),
                      PostgresSearchPattern.EscapeCharacter)
            select product.ColourCode;

        if (isIms) sampleProductColourCodes = sampleProductColourCodes.Where(_ => false);

        var visibleDetails = isIms ? _dbContext.WarehouseVoucherDetails.AsNoTracking() : await _stockVisibilityService.ApplyVoucherDetailsAsync(
            _dbContext.WarehouseVoucherDetails.AsNoTracking(),
            companyId,
            request.NormalizedKeyword,
            cancellationToken);

        var smartSearch = WarehouseVoucherSmartSearch.Build(headers, visibleDetails);
        headers = smartSearch.ApplyRequest(headers, request.NormalizedKeyword, request, out var smartKeyword);
        var filteredHeaders = WarehouseVoucherListQueryHelper.ApplyFilters(
            headers, request, visibleDetails, sampleProductColourCodes, normalizedStatus, includeKeyword: smartKeyword is not null, trackingOnly: isIms);
        if (await smartSearch.SuggestionsAsync<WarehouseVoucherListItemDto>(filteredHeaders, request, cancellationToken) is { } suggestions)
            return OperationResult<PagedResult<WarehouseVoucherListItemDto>>.Ok(suggestions);

        var totalCount = await filteredHeaders.CountAsync(cancellationToken);
        var pageHeaders = await filteredHeaders
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.VoucherId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        if (pageHeaders.Count == 0)
        {
            return OperationResult<PagedResult<WarehouseVoucherListItemDto>>.Ok(
                new PagedResult<WarehouseVoucherListItemDto>(
                    [],
                    totalCount,
                    request.NormalizedPageNumber,
                    request.NormalizedPageSize));
        }

        var voucherIds = pageHeaders.Select(x => x.VoucherId).ToArray();
        var detailEntities = await _dbContext.WarehouseVoucherDetails
            .AsNoTracking()
            .Where(x => voucherIds.Contains(x.VoucherId))
            .OrderBy(x => x.VoucherId)
            .ThenBy(x => x.LineNo)
            .ToListAsync(cancellationToken);
        var voucherDetailIds = detailEntities.Select(x => x.VoucherDetailId).ToArray();
        var movementDates = await _dbContext.WarehouseShelfLedgers
            .AsNoTracking()
            .Where(x => (!isIms || x.CompanyId == companyId) && x.VoucherDetailId != null && voucherDetailIds.Contains(x.VoucherDetailId.Value))
            .GroupBy(x => x.VoucherDetailId!.Value)
            .Select(x => new
            {
                VoucherDetailId = x.Key,
                MovementDate = x.Max(y => y.CreatedAt)
            })
            .ToDictionaryAsync(x => x.VoucherDetailId, x => x.MovementDate, cancellationToken);
        var mappedDetails = detailEntities
            .Select(x => WarehouseVoucherReadModelMapper.ToDetailDto(
                x,
                movementDates.GetValueOrDefault(x.VoucherDetailId), isIms))
            .ToList();
        if (!isIms) await WarehouseDefectReasonReader.EnrichVouchersAsync(_dbContext, companyId, mappedDetails, cancellationToken);
        var detailsByVoucher = mappedDetails
            .GroupBy(x => x.VoucherId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<WarehouseVoucherDetailDto>)x.ToList());

        var employeeIds = pageHeaders.Select(x => x.CreatedBy).Distinct().ToArray();
        var companyNames = await _dbContext.Companies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToDictionaryAsync(x => x.CompanyId, x => x.Name, cancellationToken);
        var employeeNames = await _dbContext.Employees
            .AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.CompanyId == companyId)
            .ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, cancellationToken);
        var businessInfo = await _businessReader.ReadAsync(companyId,
            pageHeaders.Select(x => new WarehouseVoucherBusinessInput(x.VoucherId, x.VoucherType, x.CodeFromRequest)).ToArray(),
            cancellationToken);

        var items = pageHeaders.Select(header =>
        {
            var business = businessInfo[header.VoucherId];
            return new WarehouseVoucherListItemDto
            {
                VoucherId = header.VoucherId,
                VoucherCode = header.VoucherCode,
                VoucherType = header.VoucherType,
                BusinessType = business.BusinessType,
                PurchaseOrderCode = business.PurchaseOrderCode,
                CanViewSupplier = business.CanViewSupplier,
                Status = header.Status,
                CreatedDate = header.CreatedDate,
                RequestId = header.RequestId,
                RequestCode = header.RequestCode,
                RequestName = isIms ? string.Empty : header.RequestName,
                ReqStatus = header.ReqStatus,
                ReqType = header.ReqType,
                CodeFromRequest = header.CodeFromRequest,
                CompanyId = header.CompanyId,
                CompanyName = companyNames.GetValueOrDefault(header.CompanyId, string.Empty),
                CreatedBy = header.CreatedBy,
                CreatedByName = employeeNames.GetValueOrDefault(header.CreatedBy, string.Empty),
                SupplierName = business.SupplierName,
                SupplierExternalId = business.SupplierExternalId,
                Comments = business.Comments,
                Details = detailsByVoucher.GetValueOrDefault(header.VoucherId, [])
            };
        }).ToList();

        return OperationResult<PagedResult<WarehouseVoucherListItemDto>>.Ok(
            new PagedResult<WarehouseVoucherListItemDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }
}
