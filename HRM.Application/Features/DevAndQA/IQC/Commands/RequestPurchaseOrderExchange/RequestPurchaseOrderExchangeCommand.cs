using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.RequestPurchaseOrderExchange;

/// <summary>Chuyển DDH liên quan dòng phiếu kho sang yêu cầu đổi hàng; không thay kết quả QC.</summary>
public sealed record RequestPurchaseOrderExchangeCommand(long VoucherDetailId)
    : IRequest<PurchaseOrderExchangeDto>;
