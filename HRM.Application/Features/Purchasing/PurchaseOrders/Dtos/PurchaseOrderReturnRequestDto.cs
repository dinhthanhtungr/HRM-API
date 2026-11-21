namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

public sealed record PurchaseReturnLineDto(long VoucherDetailId, string MaterialCode, string MaterialName,
    string? LotNumber, decimal FailedQuantityKg, decimal RequestedQuantityKg, decimal RemainingQuantityKg,
    int StockType = 4, decimal ReceivedQuantityKg = 0);
public sealed record PurchaseReturnRequestDto(int RequestId, string RequestCode, int Status, DateTime CreatedDate, string? ReturnNote = null,
    decimal RequestedQuantityKg = 0, decimal PostedQuantityKg = 0, decimal OutstandingQuantityKg = 0, bool IsActive = true);
public sealed record PurchaseReturnProgressDto(decimal ReceivedQuantityKg, decimal RequestedQuantityKg,
    decimal PostedQuantityKg, decimal OutstandingQuantityKg, decimal RemainingQuantityKg);
public sealed record PurchaseReturnOptionsDto(IReadOnlyList<PurchaseReturnLineDto> Lines,
    IReadOnlyList<PurchaseReturnRequestDto> Requests, bool HasOpenRequest, PurchaseReturnProgressDto? Progress = null);
public sealed record CreatePurchaseReturnLine(long VoucherDetailId, decimal QuantityKg);
public sealed record CreatePurchaseReturnRequest(IReadOnlyList<CreatePurchaseReturnLine>? Lines, string? Reason = null, string? Content = null,
    bool IncludeAccepted = false, Guid? ClientRequestId = null);
