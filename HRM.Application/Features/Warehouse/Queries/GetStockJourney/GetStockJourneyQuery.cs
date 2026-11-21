using HRM.Application.Commons.Models;
using HRM.Application.Features.Warehouse.Dtos;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetStockJourney;

/// <summary>Traces a persisted movement's item and lot across visible shelf history, without guessing missing links.</summary>
public sealed record GetStockJourneyQuery(string MovementKey, long LedgerId, int PageNumber = 1)
    : IRequest<OperationResult<StockJourneyDto>>;
