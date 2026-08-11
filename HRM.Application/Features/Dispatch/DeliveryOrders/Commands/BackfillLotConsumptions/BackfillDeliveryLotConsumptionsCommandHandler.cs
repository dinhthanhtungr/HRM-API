using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.DeliverySchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.BackfillLotConsumptions;

internal sealed class BackfillDeliveryLotConsumptionsCommandHandler
    : IRequestHandler<
        BackfillDeliveryLotConsumptionsCommand,
        OperationResult<BackfillDeliveryLotConsumptionsResult>>
{
    private static readonly char[] LotSeparators = [',', ';', '|', '\r', '\n'];

    private readonly IDispatchWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public BackfillDeliveryLotConsumptionsCommandHandler(
        IDispatchWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<BackfillDeliveryLotConsumptionsResult>> Handle(
        BackfillDeliveryLotConsumptionsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return OperationResult<BackfillDeliveryLotConsumptionsResult>.Fail(
                "Tài khoản hiện tại chưa được liên kết với công ty.");
        }

        if (!_currentUser.IsInRole(ApplicationRoles.Admin))
        {
            return OperationResult<BackfillDeliveryLotConsumptionsResult>.Fail(
                "Bạn không có quyền chuyển dữ liệu lot giao hàng.");
        }

        var sourceRows = await _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .Where(x =>
                !x.IsAttach &&
                x.DeliveryOrder.CompanyId == companyId)
            .Select(x => new SourceRow(
                x.Id,
                x.LotNoList,
                x.Quantity,
                x.IsActive,
                x.DeliveryOrder.CreatedBy,
                x.DeliveryOrder.CreatedDate))
            .ToListAsync(cancellationToken);

        var existingRows = await _dbContext.DeliveryOrderDetailLotConsumptions
            .AsNoTracking()
            .Where(x => x.DeliveryOrderDetail.DeliveryOrder.CompanyId == companyId)
            .Select(x => new { x.DeliveryOrderDetailId, x.Quantity })
            .ToListAsync(cancellationToken);
        var consumptionQuantityByDetail = existingRows
            .GroupBy(x => x.DeliveryOrderDetailId)
            .ToDictionary(x => x.Key, x => x.Sum(row => row.Quantity));

        var skippedExistingCount = 0;
        var skippedBlankLotCount = 0;
        var skippedMultipleLotsCount = 0;
        var rowsToCreate = new List<(SourceRow Source, string LotNo)>();

        foreach (var sourceRow in sourceRows)
        {
            if (consumptionQuantityByDetail.ContainsKey(sourceRow.DeliveryOrderDetailId))
            {
                skippedExistingCount++;
                continue;
            }

            var lotNumbers = SplitLotNumbers(sourceRow.LotNoList);
            if (lotNumbers.Length == 0)
            {
                skippedBlankLotCount++;
                continue;
            }

            if (lotNumbers.Length > 1)
            {
                skippedMultipleLotsCount++;
                continue;
            }

            rowsToCreate.Add((sourceRow, lotNumbers[0]));
        }

        if (!request.DryRun)
        {
            await using var transaction = await _dbContext.BeginDeliveryOrderTransactionAsync(cancellationToken);
            var fallbackCreatedDate = DateTime.Now;

            foreach (var row in rowsToCreate)
            {
                _dbContext.DeliveryOrderDetailLotConsumptions.Add(
                    new DeliveryOrderDetailLotConsumption
                    {
                        Id = Guid.CreateVersion7(),
                        DeliveryOrderDetailId = row.Source.DeliveryOrderDetailId,
                        LotNo = row.LotNo,
                        Quantity = row.Source.Quantity,
                        UnitCostSnapshot = 0m,
                        TotalCostSnapshot = 0m,
                        CreatedBy = row.Source.CreatedBy,
                        CreatedDate = row.Source.CreatedDate ?? fallbackCreatedDate,
                        IsActive = row.Source.IsActive
                    });
            }

            if (rowsToCreate.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            foreach (var row in rowsToCreate)
            {
                consumptionQuantityByDetail[row.Source.DeliveryOrderDetailId] = row.Source.Quantity;
            }
        }

        var reconciliation = DeliveryOrderLotBackfillReconciliationRules.Build(
            sourceRows.Select(x => new DeliveryOrderLotBackfillSource(
                x.DeliveryOrderDetailId,
                x.LotNoList,
                x.Quantity)).ToArray(),
            consumptionQuantityByDetail);

        var result = new BackfillDeliveryLotConsumptionsResult(
            companyId,
            request.DryRun,
            sourceRows.Count,
            reconciliation.LegacyRowCount,
            rowsToCreate.Count,
            request.DryRun ? 0 : rowsToCreate.Count,
            skippedExistingCount,
            skippedBlankLotCount,
            skippedMultipleLotsCount,
            skippedBlankLotCount + skippedMultipleLotsCount,
            reconciliation.ConvertedRowCount,
            reconciliation.QuantityMismatchRowCount,
            reconciliation.LegacyQuantityTotal,
            reconciliation.ConsumptionQuantityTotal,
            reconciliation.QuantityDifference);

        var message = request.DryRun
            ? "Đã kiểm tra dữ liệu lot giao hàng, chưa ghi dữ liệu."
            : "Chuyển dữ liệu lot giao hàng thành công.";

        return OperationResult<BackfillDeliveryLotConsumptionsResult>.Ok(result, message);
    }

    private static string[] SplitLotNumbers(string? lotNoList)
        => string.IsNullOrWhiteSpace(lotNoList)
            ? []
            : lotNoList
                .Split(LotSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private sealed record SourceRow(
        Guid DeliveryOrderDetailId,
        string? LotNoList,
        decimal Quantity,
        bool IsActive,
        Guid CreatedBy,
        DateTime? CreatedDate);
}
