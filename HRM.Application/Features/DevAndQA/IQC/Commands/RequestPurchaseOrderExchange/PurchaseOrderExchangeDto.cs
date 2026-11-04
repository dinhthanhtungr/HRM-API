namespace HRM.Application.Features.DevAndQA.IQC.Commands.RequestPurchaseOrderExchange;

/// <summary>DDH đã nhận yêu cầu đổi hàng; Status là mã để FE hiển thị Y/C đổi hàng.</summary>
public sealed record PurchaseOrderExchangeDto(Guid PurchaseOrderId, string? ExternalId, string Status);
