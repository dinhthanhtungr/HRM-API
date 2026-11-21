using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;

public sealed record GetProductionTrackingQuery(Guid Id) : IRequest<ProductionTrackingDto?>;
