using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.BackfillLotConsumptions;

/// <summary>
/// Chuyển dữ liệu lot lịch sử từ DeliveryOrderDetail.LotNoList sang bảng lot consumption mới.
/// </summary>
public sealed record BackfillDeliveryLotConsumptionsCommand(
    bool DryRun = true) : IRequest<OperationResult<BackfillDeliveryLotConsumptionsResult>>;

public sealed record BackfillDeliveryLotConsumptionsResult(
    Guid CompanyId,
    bool DryRun,
    int SourceRowCount,
    int LegacyRowCount,
    int WouldCreateCount,
    int CreatedCount,
    int AlreadyConvertedCount,
    int SkippedBlankLotCount,
    int SkippedMultipleLotsCount,
    int SkippedRowCount,
    int ConvertedRowCount,
    int QuantityMismatchRowCount,
    decimal LegacyQuantityTotal,
    decimal ConsumptionQuantityTotal,
    decimal QuantityDifference);
