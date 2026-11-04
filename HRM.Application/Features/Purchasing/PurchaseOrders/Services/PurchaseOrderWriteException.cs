namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

public sealed class PurchaseOrderWriteException(string message) : InvalidOperationException(message);
