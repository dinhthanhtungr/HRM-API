using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrderNotes;

public sealed record UpdatePurchaseOrderNotesCommand(Guid PurchaseOrderId, UpdatePurchaseOrderNotesRequest Request)
    : IRequest<OperationResult>;
