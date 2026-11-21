using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using MediatR;

namespace HRM.Application.Features.PLM.MaterialProcessing.Queries;

public sealed record ProcessingStockQuery(Guid MaterialId, int ItemType = 0, Guid? OrderId = null) : IRequest<ProcessingStockDto?>;
internal sealed class ProcessingStockQueryHandler(ProcessingWarehouseService warehouse, ICurrentUser user)
    : IRequestHandler<ProcessingStockQuery, ProcessingStockDto?>
{
    public Task<ProcessingStockDto?> Handle(ProcessingStockQuery request, CancellationToken ct)
        => warehouse.StockAsync(request.MaterialId, request.ItemType, request.OrderId, user, ct);
}
